using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PilotAIAssistantControl {
	/// <summary>
	/// Reads the GitHub Copilot OAuth token out of the SQLite <c>auth.db</c> that newer Copilot
	/// clients use. Copilot migrated away from the plain-text apps.json / hosts.json files - a
	/// migrated install carries a <c>legacy_files_migration_done</c> marker in auth.db's metadata
	/// table, and the legacy files are left behind stale (or absent on a fresh install).
	/// </summary>
	internal static class CopilotAuthDb {
		/// <summary>
		/// Prefer the token attached to an active editor session, then the most recently used one.
		/// Only schema version 0 is readable - that is the plain-text form. If GitHub starts
		/// encrypting the blob it will bump the version, and we skip those rows rather than
		/// handing back ciphertext that would fail against the API.
		/// </summary>
		private const string TokenQuery = @"
			SELECT t.token_ciphertext
			FROM oauth_tokens t
			LEFT JOIN active_sessions s ON s.token_id = t.token_id
			WHERE t.token_schema_version = 0
			ORDER BY (s.token_id IS NOT NULL) DESC, t.last_used_at DESC
			LIMIT 1";

		/// <summary>
		/// Attempts to read an OAuth token from the given auth.db.
		/// </summary>
		/// <param name="dbPath">Full path to auth.db.</param>
		/// <returns>The token, or null if unavailable for any reason.</returns>
		public static string? TryReadToken(string dbPath) {
			if (!File.Exists(dbPath))
				return null;

			// Copilot keeps auth.db open with WAL journalling, so the newest token can live in the
			// -wal sidecar rather than the main file. Copy all three to a scratch directory and read
			// the snapshot - that keeps SQLite's recovery logic intact without our touching the
			// files another process owns.
			string? scratch = null;
			try {
				scratch = Path.Combine(Path.GetTempPath(), "pilotai-copilot-auth-" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(scratch);

				var target = Path.Combine(scratch, "auth.db");
				File.Copy(dbPath, target, overwrite: true);
				foreach (var suffix in new[] { "-wal", "-shm" }) {
					var sidecar = dbPath + suffix;
					if (File.Exists(sidecar))
						File.Copy(sidecar, target + suffix, overwrite: true);
				}

				return ReadTokenFrom(target);
			} catch {
				// A schema change, a locked file, a copy failure - fall back to the legacy files.
				return null;
			} finally {
				try {
					if (scratch != null && Directory.Exists(scratch))
						Directory.Delete(scratch, recursive: true);
				} catch {
					// Best effort cleanup only.
				}
			}
		}

		private static string? ReadTokenFrom(string dbPath) {
			// ReadWrite rather than ReadOnly: SQLite needs to write the -shm and may need to replay
			// the -wal to expose the newest rows. This is our private copy, so that is safe.
			var connectionString = new SqliteConnectionStringBuilder {
				DataSource = dbPath,
				Mode = SqliteOpenMode.ReadWrite,
				Pooling = false
			}.ToString();

			using var connection = new SqliteConnection(connectionString);
			connection.Open();

			using var command = connection.CreateCommand();
			command.CommandText = TokenQuery;

			// The column is declared BLOB but holds the token as UTF-8 text.
			var value = command.ExecuteScalar();
			var token = value switch {
				byte[] blob => System.Text.Encoding.UTF8.GetString(blob),
				string text => text,
				_ => null
			};

			token = token?.Trim();
			return string.IsNullOrEmpty(token) ? null : token;
		}
	}
}

# Judo Tournament Management Linux release

This folder is a ready-to-run `linux-x64` package. It contains the self-contained
application in `app/`, plus systemd and nginx configuration in `deploy/`.
No .NET SDK or Node.js installation is required on the target host.

## One-command install (recommended)

On a fresh, minimal Debian/Ubuntu LXC host you can go from "empty container" to
"running app" with a single command. It installs its own prerequisites
(`curl`, `unzip`, `ca-certificates`), downloads the latest release, verifies its
checksum, and runs the bundled installer for you:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/JudoTournamentManager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
```

Pin a specific release with `--version`:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/JudoTournamentManager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --version v1.2.3 --hostname tournament.example.com --email admin@example.com
```

Every option other than `--version` (for example `--hostname`, `--email`,
`--skip-certbot`, `--install-dir`) is forwarded unchanged to the installer.
Re-running the same command (default latest, or a newer `--version`) upgrades in
place; the installer preserves the SQLite database under `app/App_Data/`.

### Inspect before you run

Piping a remote script straight into `sudo bash` requires trusting it sight
unseen. To review it first, download, read, then execute:

```bash
curl -fsSL -o bootstrap_install.sh \
  https://raw.githubusercontent.com/McGyver666/JudoTournamentManager/main/deploy/bootstrap_install.sh
less bootstrap_install.sh
sudo bash bootstrap_install.sh --hostname tournament.example.com --email admin@example.com
```

## Manual install on Debian/Ubuntu LXC

1. Copy and extract `release.zip` on the LXC host.
2. Run the bundled installer from the extracted `release/` folder:

   ```bash
   chmod +x deploy/install_release.sh
   sudo ./deploy/install_release.sh --hostname tournament.example.com --email admin@example.com
   ```

   It installs nginx, creates the `judo` service account and an application
   secret, copies the app to `/opt/judo-tournament`, preserves any existing
   SQLite database, enables the systemd service, and requests a TLS certificate.

   The hostname must already resolve publicly to the LXC host and ports 80/443
   must be reachable for Let's Encrypt. Use `--skip-certbot` for an HTTP-only
   installation or when TLS is terminated by another proxy.

For an upgrade, extract the new release and rerun the same command. The installer
does not overwrite `app/App_Data/`, which contains the SQLite database.

The SQLite database is created at `/opt/judo-tournament/app/App_Data/` and must be
included in backups and retained when upgrading. On an upgrade, stop the service,
replace `app/` and `deploy/`, preserve `app/App_Data/`, then start the service.

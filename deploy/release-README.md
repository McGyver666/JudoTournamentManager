# Judo Tournament Management Linux release

This folder is a ready-to-run `linux-x64` package. It contains the self-contained
application in `app/`, plus systemd and nginx configuration in `deploy/`.
No .NET SDK or Node.js installation is required on the target host.

## Install on Debian/Ubuntu LXC

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

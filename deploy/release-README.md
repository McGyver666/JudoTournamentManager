# Shiai Manager Linux release

This folder is a ready-to-run self-contained package for either `linux-x64` or
`linux-arm64`. The release pipeline publishes one archive per architecture and
the bootstrap installer selects the matching package automatically. It contains
the application in `app/`, plus systemd and nginx configuration in `deploy/`.
No .NET SDK or Node.js installation is required on the target host.

## One-command install (recommended)

On a fresh, minimal Debian/Ubuntu or RHEL-compatible LXC host you can go from
"empty container" to
"running app" with a single command once the bootstrap fetch prerequisites are
available. The bootstrap script installs its own runtime prerequisites inside
the download/verification phase, but the outer shell still needs a downloader
and CA certificates available on the host.

Debian/Ubuntu prerequisites:

```bash
sudo apt-get update
sudo apt-get install -y curl ca-certificates unzip
```

RHEL-compatible prerequisites, including Oracle Linux 10:

```bash
sudo dnf install -y curl ca-certificates unzip
```

Run the same bootstrap command on either x64 or ARM64 Linux:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
```

Pin a specific release with `--version`:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
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
  https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh
less bootstrap_install.sh
sudo bash bootstrap_install.sh --hostname tournament.example.com --email admin@example.com
```

## Manual install on Debian/Ubuntu or RHEL-compatible LXC

The bundled installer detects the host distribution automatically. On RHEL-compatible
hosts, including Oracle Linux 10, it uses `dnf` or `yum`, places the nginx site in
`/etc/nginx/conf.d/`, and enables the required SELinux proxy permission when SELinux
is enforcing. Certbot is installed from the configured repositories; if Certbot is
not available, enable EPEL or use `--skip-certbot`.

1. Copy and extract the archive matching the host architecture on the LXC host:
  `release-linux-x64.zip` for x86_64 or `release-linux-arm64.zip` for ARM64.
2. Run the bundled installer from the extracted `release-linux-x64/` or
  `release-linux-arm64/` folder:

   ```bash
   chmod +x deploy/install_release.sh
   sudo ./deploy/install_release.sh --hostname tournament.example.com --email admin@example.com
   ```

   It installs nginx, creates the `shiai` service account and an application
   secret, copies the app to `/opt/shiai-manager`, preserves any existing
   SQLite database, enables the systemd service, and requests a TLS certificate.

   On a fresh install it also creates an initial `admin` account with a random
   password and prints it once at the end, in a clearly marked block — save it,
   as it is not shown again. Re-running (an upgrade) leaves the existing account
   untouched and prints nothing.

   The hostname must already resolve publicly to the LXC host and ports 80/443
   must be reachable for Let's Encrypt. Use `--skip-certbot` for an HTTP-only
   installation or when TLS is terminated by another proxy.

For an upgrade, extract the new release and rerun the same command. The installer
does not overwrite `app/App_Data/`, which contains the SQLite database.

The SQLite database is created at `/opt/shiai-manager/app/App_Data/` and must be
included in backups and retained when upgrading. On an upgrade, stop the service,
replace `app/` and `deploy/`, preserve `app/App_Data/`, then start the service.

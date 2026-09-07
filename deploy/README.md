# Deployment notes for Proxmox/LXC

These files support Debian/Ubuntu and RHEL-compatible containers that host the Shiai Manager app.

The bundled `install_release.sh` installer also supports RHEL-compatible hosts,
including Oracle Linux 10. It selects `dnf` or `yum`, writes nginx configuration
to `/etc/nginx/conf.d/`, and enables the SELinux nginx-to-API permission when
SELinux is enforcing. The manual commands below remain Debian/Ubuntu examples.

## One-command install

For a released build, the fastest path is the bootstrap script, which downloads
the latest (or a pinned `--version vX.Y.Z`) GitHub release, verifies its
checksum for the matching architecture, and runs `install_release.sh` for you.
The release pipeline publishes `release-linux-x64.zip` and
`release-linux-arm64.zip`; the bootstrap script selects the correct one from
the host's `uname -m` value.

On a fresh host, install the bootstrap downloader prerequisites with the host's
package manager first.

Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y curl ca-certificates unzip
```

RHEL-compatible systems, including Oracle Linux 10:

```bash
sudo dnf install -y curl ca-certificates unzip
```

Then run the bootstrap command on either x64 or ARM64 Linux:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
```

Prefer to review the script first? Download it, inspect it, then run it locally
instead of piping into `sudo bash`. See `release-README.md` for the full
`bootstrap_install.sh` reference and the inspect-before-run alternative.

On a fresh install, `install_release.sh` also creates an initial `admin` account
with a random password (>= 12 chars, mixed case + digits + special) and prints
it once at the very end in a clearly marked block — save it, it is not shown
again. Re-running on an existing install leaves the account untouched and prints
nothing.

The manual steps below remain available for source-based or customised installs.

## Files
- `shiai-manager.service`: systemd unit for the ASP.NET Core API
- `shiai-manager.nginx.conf`: nginx reverse proxy config for HTTP/HTTPS

## Assumptions
- The application is deployed under `/opt/shiai-manager`
- The API will listen on `127.0.0.1:5080`
- nginx terminates TLS and forwards requests to the app
- The public hostname is provided at deploy time via the `DOMAIN` variable (the nginx config ships with a `__SERVER_NAME__` placeholder)

## 1) Install prerequisites
```bash
sudo apt update
sudo apt install -y dotnet-sdk-10 nginx certbot python3-certbot-nginx ufw
```

## 2) Create service user
```bash
sudo useradd --system --create-home --home-dir /opt/shiai-manager --shell /usr/sbin/nologin shiai
```

If you want the app to build the frontend before publishing, also install Node.js/npm in the container:
```bash
sudo apt install -y nodejs npm
```

## 3) Copy the app to the container
```bash
sudo mkdir -p /opt/shiai-manager
sudo rsync -a /path/to/your/repo/ /opt/shiai-manager/
```

## 4) Copy service and nginx config
Set `DOMAIN` to your public hostname; it is substituted into the nginx config's `__SERVER_NAME__` placeholder.
```bash
export DOMAIN=tournament.example.com
sudo cp /opt/shiai-manager/deploy/shiai-manager.service /etc/systemd/system/shiai-manager.service
sudo cp /opt/shiai-manager/deploy/shiai-manager.nginx.conf /etc/nginx/sites-available/shiai-manager
sudo sed -i "s/__SERVER_NAME__/$DOMAIN/g" /etc/nginx/sites-available/shiai-manager
sudo ln -s /etc/nginx/sites-available/shiai-manager /etc/nginx/sites-enabled/shiai-manager
```

## 5) Create environment file for the service
```bash
sudo mkdir -p /etc/default
sudo tee /etc/default/shiai-manager > /dev/null <<'EOF'
Security__AuthTokenHmacSecret=replace-with-a-long-random-secret
EOF
```

## 6) Obtain TLS certificate
Use the HTTP-only nginx config above for the first run. Certbot will then add the HTTPS server block and the certificate paths for you.
```bash
sudo certbot --nginx -d "$DOMAIN"
```

## 7) Enable and start services
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now shiai-manager nginx
sudo systemctl status shiai-manager nginx --no-pager
```

## 8) Open firewall ports
```bash
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

## Notes
- The API is expected to serve the built Angular frontend from its `wwwroot` directory.
- The app uses SQLite, so keep `/opt/shiai-manager/ShiaiManager.Api/App_Data` on persistent storage if the container is rebuilt.
- If you want to avoid publishing the app from source, you can replace the `ExecStartPre` line with a pre-built deployment directory.

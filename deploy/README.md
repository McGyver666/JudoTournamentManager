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

## Optional: CrowdSec protection

Append `--with-crowdsec` to the bootstrap (or `install_release.sh`) command to
also install [CrowdSec](https://docs.crowdsec.net/):

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com --with-crowdsec
```

What the installer sets up:
- CrowdSec Security Engine and the Lua-based `crowdsec-nginx-bouncer` from the
  official packagecloud repository (GPG key in `/etc/apt/keyrings/`, bound to
  that repository via `signed-by`).
- Collections `crowdsecurity/nginx`, `crowdsecurity/base-http-scenarios` and
  `crowdsecurity/http-cve`. The volume-based scenarios
  `crowdsecurity/http-crawl-non_statics` and `crowdsecurity/http-probing` are
  removed so that a venue whose devices share one public IP is not banned
  mid-tournament; the app already rate-limits login and public endpoints per IP.
- The AppSec (WAF) component with `crowdsecurity/appsec-virtual-patching` only
  (rules for known CVEs), listening on `127.0.0.1:7422`.
- A German ban page (`deploy/crowdsec-ban.html`), configured through
  `/etc/crowdsec/bouncers/crowdsec-nginx-bouncer.conf.local`.

Requirements and limits:
- **Debian 12+ or Ubuntu 24.04+ only.** The bouncer needs the nginx Lua module,
  which is not available on RHEL-compatible hosts and broken on Ubuntu 22.04. On
  unsupported hosts the installer stops before installing anything.
- **nginx must be the internet-facing edge.** Do not put a CDN, tunnel or
  another reverse proxy in front of it; plain NAT/port forwarding is fine.
  Behind a proxy every request appears to come from the proxy's IP, so the first
  ban would lock out all users.
- The bouncer fails open: if CrowdSec is unavailable, requests are allowed.
- CrowdSec's Central API stays enabled. The engine shares signals about detected
  attackers (IP, scenario, timestamp) with CrowdSec and receives the community
  blocklist in return; regular user traffic is not shared. To opt out, comment
  out the `api.server.online_client` section in `/etc/crowdsec/config.yaml` and
  run `sudo systemctl restart crowdsec`.
- Re-running the installer with `--with-crowdsec` re-applies this setup.
  Re-running without it leaves an existing CrowdSec installation unchanged.

Day-to-day commands:

```bash
sudo cscli decisions list                        # active bans
sudo cscli decisions delete --ip 203.0.113.10    # emergency unban, e.g. the venue's IP
sudo cscli alerts list
sudo cscli metrics
```

Uninstall:

```bash
sudo apt-get purge -y crowdsec-nginx-bouncer crowdsec
sudo rm -rf /etc/crowdsec
sudo rm -f /etc/apt/sources.list.d/crowdsec_crowdsec.list /etc/apt/keyrings/crowdsec_crowdsec-archive-keyring.gpg
sudo nginx -t && sudo systemctl reload nginx
```

## Files
- `shiai-manager.service`: systemd unit for the ASP.NET Core API
- `shiai-manager.nginx.conf`: nginx reverse proxy config for HTTP/HTTPS
- `crowdsec-ban.html`: German ban page used by the optional CrowdSec nginx bouncer

## Assumptions
- The application is deployed under `/opt/shiai-manager`
- The API will listen on `127.0.0.1:5080`
- nginx terminates TLS and forwards requests to the app
- nginx limits request bodies to 20 MB, except backup restore (`POST /api/tournaments/restore`), which allows up to 50 MB to match the API limit. Re-running the installer applies this to existing hosts.
- The public hostname is provided at deploy time via the `DOMAIN` variable (the nginx config ships with a `__SERVER_NAME__` placeholder)
- Production host validation uses `AllowedHosts`; the installer sets it to the supplied hostname.
- Guest-share URLs use the canonical `GuestShare__PublicBaseUrl` configured by the installer instead of the incoming `Host` header.

## 1) Install prerequisites
```bash
sudo apt update
sudo apt install -y dotnet-sdk-10 nginx certbot python3-certbot-nginx ufw
```

## 2) Create service user
```bash
sudo useradd --system --create-home --home-dir /opt/shiai-manager --shell /usr/sbin/nologin shiai
```

For a source-based deployment, build the frontend before publishing. The systemd unit publishes
the API but does not run npm, so install Node.js/npm and build the Angular app explicitly. Angular 22
needs Node.js `^22.22.3`, `^24.15.0` or `>=26`; the Debian `nodejs` package is older, so install a
supported release (for example from NodeSource):

```bash
node --version  # must satisfy the range above
cd /opt/shiai-manager/frontend
npm install
npm run build
```

The build writes the frontend to `/opt/shiai-manager/ShiaiManager.Api/wwwroot`. The release archive
already contains the built frontend and does not require Node.js/npm on the target host.

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
AllowedHosts=tournament.example.com
GuestShare__PublicBaseUrl=https://tournament.example.com
EOF
```

`AllowedHosts` must contain the public hostname used by nginx. `GuestShare__PublicBaseUrl`
is the canonical HTTPS origin used in guest links and QR codes. The bundled installer
writes both values from `--hostname` and preserves them on upgrades when they already exist.

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
- nginx rejects unmatched HTTP and HTTPS hostnames with status `444` before proxying to the API.
- The app uses SQLite, so keep `/opt/shiai-manager/ShiaiManager.Api/App_Data` on persistent storage if the container is rebuilt.
- If you want to avoid publishing the app from source, you can replace the `ExecStartPre` line with a pre-built deployment directory.

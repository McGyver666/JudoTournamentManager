# Deployment notes for Proxmox/LXC

These files are intended for a Debian/Ubuntu container that will host the Judo Tournament Management app.

## Files
- `judo-tournament.service`: systemd unit for the ASP.NET Core API
- `judo-tournament.nginx.conf`: nginx reverse proxy config for HTTP/HTTPS

## Assumptions
- The application is deployed under `/opt/judo-tournament`
- The API will listen on `127.0.0.1:5080`
- nginx terminates TLS and forwards requests to the app
- The public hostname is `tournament.example.com`

## 1) Install prerequisites
```bash
sudo apt update
sudo apt install -y dotnet-sdk-10 nginx certbot python3-certbot-nginx ufw
```

## 2) Create service user
```bash
sudo useradd --system --create-home --home-dir /opt/judo-tournament --shell /usr/sbin/nologin judo
```

If you want the app to build the frontend before publishing, also install Node.js/npm in the container:
```bash
sudo apt install -y nodejs npm
```

## 3) Copy the app to the container
```bash
sudo mkdir -p /opt/judo-tournament
sudo rsync -a /path/to/your/repo/ /opt/judo-tournament/
```

## 4) Copy service and nginx config
```bash
sudo cp /opt/judo-tournament/deploy/judo-tournament.service /etc/systemd/system/judo-tournament.service
sudo cp /opt/judo-tournament/deploy/judo-tournament.nginx.conf /etc/nginx/sites-available/judo-tournament
sudo ln -s /etc/nginx/sites-available/judo-tournament /etc/nginx/sites-enabled/judo-tournament
```

## 5) Create environment file for the service
```bash
sudo mkdir -p /etc/default
sudo tee /etc/default/judo-tournament > /dev/null <<'EOF'
Security__AuthTokenHmacSecret=replace-with-a-long-random-secret
EOF
```

## 6) Obtain TLS certificate
Use the HTTP-only nginx config above for the first run. Certbot will then add the HTTPS server block and the certificate paths for you.
```bash
sudo certbot --nginx -d janet.duckdns.org
```

## 7) Enable and start services
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now judo-tournament nginx
sudo systemctl status judo-tournament nginx --no-pager
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
- The app uses SQLite, so keep `/opt/judo-tournament/JudoTournamentManagement.Api/App_Data` on persistent storage if the container is rebuilt.
- If you want to avoid publishing the app from source, you can replace the `ExecStartPre` line with a pre-built deployment directory.

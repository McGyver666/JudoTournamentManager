# Shiai Manager frontend

The Angular 19 single-page application for Shiai Manager lives in this directory. The API serves
the compiled application from `ShiaiManager.Api/wwwroot`, so no separate web server is required
for normal local or production operation.

## Prerequisites

- Node.js and npm
- A running Shiai Manager API for the development proxy

Install dependencies once:

```bash
npm install
```

## Development server

Start Angular with the configured proxy to the local API:

```bash
npm start
```

Open `http://localhost:4200/`. The proxy configuration is in `proxy.conf.json`; the API normally
runs at `http://localhost:5080`.

## Build

Build the production frontend directly into the API's `wwwroot` directory:

```bash
npm run build
```

The repository root scripts (`start-local.ps1` and `start-local.sh`) run this build automatically
unless the frontend build is explicitly skipped and existing `wwwroot` files are available.

## Unit tests

Run tests interactively with Karma:

```bash
npm test
```

Run the headless CI form, which exits automatically:

```bash
npm run test:ci
```

There is currently no end-to-end test script configured in this project.

## Application routes

The main routes are `/tournaments`, `/tournament-overview`, `/config`, `/registrations`,
`/category-assignment`, `/draw`, `/tatami-assignment`, `/team-matchday`, `/combat-overview`,
`/match`, `/results`, `/display`, `/display/match-lists`, `/users`, and `/public/match-lists`.
Authentication guards enforce the role and tournament-context requirements in the application.

Localization files are served from `public/i18n/`; German (`de.json`) is the source language and
English (`en.json`) is the fallback.

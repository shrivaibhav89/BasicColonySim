# Nakama on Render (Free Web Service)

This folder is a minimal deploy package for running Nakama on Render.

## 1) Push this folder to GitHub

Keep these files in `backend/nakama-render/`:
- `Dockerfile`
- `start.sh`

## 2) Create Render Postgres (Free)

In Render dashboard:
- New -> Postgres
- Plan: Free
- Region: choose one close to your players

Note: Free Postgres expires after 30 days.

## 3) Create Render Web Service

In Render dashboard:
- New -> Web Service
- Connect your repo
- Root Directory: `backend/nakama-render`
- Runtime: Docker
- Plan: Free
- Region: same as Postgres
- Port: keep default (`10000`) or set your own public port value

## 4) Add environment variables

Required (pick one):
- `DATABASE_URL` (easiest)
- `NAKAMA_DB_ADDRESS`

`DATABASE_URL` value:
- Copy Render Postgres **Internal Database URL** directly.

`NAKAMA_DB_ADDRESS` value format:
`user:password@host:5432/database?sslmode=require`

Get these values from your Render Postgres "Internal connection" info.

Security keys (set your own values):
- `NAKAMA_SERVER_KEY`
- `NAKAMA_SESSION_ENCRYPTION_KEY`
- `NAKAMA_SESSION_REFRESH_ENCRYPTION_KEY`
- `NAKAMA_HTTP_KEY`
- `NAKAMA_CONSOLE_USER`
- `NAKAMA_CONSOLE_PASS`

Optional:
- `NAKAMA_NODE_NAME`

## 5) Deploy

Render will run migration first, then start Nakama.

## 6) Unity client connection

Use hosted URL + TLS:
- scheme: `https`
- host: `<your-service>.onrender.com`
- port: `443`
- server key: your `NAKAMA_SERVER_KEY`

For socket (realtime) use `wss`.

## Important free-tier limits

Render free web services:
- spin down after 15 min idle
- ~1 minute cold start when waking up
- single instance only

Good for prototype, not production.
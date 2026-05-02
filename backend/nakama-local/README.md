# Local Nakama (Docker)

This runs Nakama + Postgres locally on your machine.

## Start

```powershell
cd F:\ColonySim\BasicColonySim\backend\nakama-local
docker compose up -d
```

## Check

```powershell
docker compose ps
docker compose logs -f nakama
```

Nakama endpoints:
- API: `http://127.0.0.1:7350`
- Console: `http://127.0.0.1:7351`

Console login:
- user: `admin`
- pass: `password`

## Unity client config (local)

- scheme: `http`
- host: `127.0.0.1`
- port: `7350`
- server key: `colony_dev_server_key`

Socket uses `ws://127.0.0.1:7350`.

## Stop

```powershell
docker compose down
```

To also delete database data:

```powershell
docker compose down -v
```

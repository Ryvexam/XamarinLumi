# LumiContact Backend

Le backend LumiContact fournit trois briques:

- une API HTTP pour lire et modifier les contacts
- une persistence SQLite serveur
- une diffusion temps reel via SignalR et WebSocket

Le client Xamarin utilise:

- HTTP pour les donnees
- WebSocket brut sur `/ws/contacts` pour les notifications temps reel

Le hub `SignalR` reste disponible sur `/hubs/contacts`.

## Configuration

Copier `.env.example` vers `.env` puis definir:

- `SYNC_PUBLIC_BASE_URL=https://contact.ryvexam.fr`
- `SYNC_APP_KEY=lumicontact-public-app`
- `SYNC_CONNECTION_STRING=Data Source=/data/lumicontact.db`

## Execution locale

```bash
cd /Users/maximevery/Projects/LumiContactApp/backend/LumiContact.Backend
dotnet run
```

## Execution avec Docker

```bash
cd /Users/maximevery/Projects/LumiContactApp/backend
cp .env.example .env
docker compose up -d --build
```

Le container ecoute sur `http://0.0.0.0:8080`. En production, il doit etre place derriere un reverse proxy et `SYNC_PUBLIC_BASE_URL` doit etre fixe a l'URL finale publique.

## Authentification legere

Toutes les routes `/api/*`, `/hubs/*` et `/ws/*` sont protegees par une simple verification d'app key:

```text
X-Lumi-App-Key: lumicontact-public-app
```

Ce n'est pas une authentification utilisateur. Le carnet est volontairement public pour tous les clients de l'application.

## Endpoints

HTTP:

- `GET /api/health`
- `GET /api/settings`
- `GET /api/contacts`
- `POST /api/contacts`
- `PUT /api/contacts/{id}`
- `DELETE /api/contacts/{id}`

Temps reel:

- `GET /hubs/contacts`
- `GET /ws/contacts`

## Fonctionnement temps reel

Quand le backend recoit:

- une creation de contact
- une mise a jour
- une suppression

il:

1. persiste le changement en base
2. broadcast un message `ContactsChanged` sur SignalR
3. broadcast le meme evenement JSON a tous les clients WebSocket

Le client mobile recoit l'evenement, puis relance un `GET /api/contacts` pour se resynchroniser proprement.

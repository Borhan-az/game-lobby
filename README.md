Multiplayer Game Lobby System

This project is a REST API for a multiplayer game lobby system with real-time chat, designed for clustered environments using Redis.

To build a scalable lobby system where players can join lobbies (max 64 players), get notified, and chat in real-time. It runs across multiple application instances (pods) using Redis for state management.
Architecture

The system uses a Clean Architecture with four main layers:

    API Layer (LobbySystem.Api): Handles web requests (HTTP, WebSockets) and serves the client.

    Application Layer (LobbySystem.Application): Contains the main business logic and use cases (e.g., how to join a lobby).

    Contract Layer (LobbySystem.Contract): Defines interfaces and data models shared between layers.

    Infrastructure Layer (LobbySystem.Infrastructure): Implements external concerns like Redis data access and distributed locks.

Clustered Environment Handling:

    Redis as Single Source of Truth: All lobby data is in Redis.

    Distributed Locks (RedLock.Net): Prevents conflicts when multiple servers try to update the same data.

    Atomic Redis Operations (Lua Scripts): Ensures complex Redis operations are completed without interruption.

    SignalR Redis Backplane: Automatically distributes chat messages across all connected servers/pods.
    
How to Run
Prerequisites

    .NET 8 SDK: Install from dotnet.microsoft.com.

    Redis Server: Run locally using Docker Desktop.

    Docker Desktop: Ensure Docker Desktop is installed and running.

Steps

    Run Application with Docker Compose:

        Ensure Docker Desktop is running.

        Navigate to the root directory of your solution (where docker-compose.yml is located):

        cd path/to/your/LobbySystem

        Build and start all services (Redis and your .NET API):

        docker compose up --build

        (The --build flag ensures your .NET API image is rebuilt if changes are detected. You can omit it on subsequent runs if no code changes.)

        To stop the services later, press Ctrl+C in the terminal and then run:

        docker compose down

    Access Clients:
    Once docker compose up is running, your application will be accessible:

        Lobby System & Chat Client (HTML): Go to http://localhost:7001/index.html

        API (Swagger): Go to http://localhost:7001/swagger

API Endpoints

    POST /api/lobbies/join

        Purpose: Player joins a lobby. System finds/creates if no preferred ID.

        Body: {"playerId": "Player123", "preferredLobbyId": null}

        Response: 200 OK (joined) or 409 Conflict (full/blocked).

    GET /api/lobbies/list

        Purpose: Get a list of active lobbies.

        Response: 200 OK with [{"lobbyId": "...", "currentPlayers": ..., "maxCapacity": ...}].

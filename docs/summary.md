# Agent Observability for Azure App Service — Summary & Vision

## What we built

We created **Azure.AppService.AgentDashboard**, a .NET NuGet package that gives real-time observability into AI agent workloads running on App Service. It works by inserting lightweight middleware into the `IChatClient` pipeline (the standard .NET AI abstraction from `Microsoft.Extensions.AI`) and automatically captures every LLM call — which agent made it, how long it took, how many tokens it consumed, whether it succeeded, and what tools were invoked.

The package serves an embedded HTML dashboard (no static files to deploy) with:
- **Agent topology** — a visual graph of how agents are organized into workflow phases
- **Per-agent metrics** — invocation count, average/P95 latency, token usage, error rates
- **Trace log** — chronological record of every LLM call with full metadata
- **JSON API endpoints** — all data available programmatically

It also solves a real architectural challenge on App Service: when agents run in a WebJob (background process) but the dashboard is served from the API (main site process), telemetry needs to cross process boundaries. We handle this via a shared JSONL file on the App Service filesystem (`$HOME/LogFiles/`), which both processes can read and write — no external dependencies.

We validated this by integrating it into a [multi-agent travel planner demo app](https://github.com/seligj95/app-service-hosted-agent-dashboard) built with the Microsoft Agent Framework (MAF). Six specialized agents (Coordinator, CurrencyConverter, WeatherAdvisor, LocalKnowledge, ItineraryPlanner, BudgetOptimizer) run across four workflow phases. The dashboard correctly captures all agent activity across the API and WebJob processes.

## Why this matters

**Today, App Service has no way to tell customers anything about their agent workloads.** A customer deploys an agentic app, and from the platform's perspective it's just another web app. We can't answer:
- How many agents are running?
- How are they performing?
- How many tokens are they burning (i.e., what's the cost)?
- Are any failing?
- What's the execution flow?

This is a gap — especially as agent workloads become a primary use case for App Service.

## Where this could go

### 1. Platform-native agent observability (Portal / Kudu / SCM)

The current package requires a NuGet reference and 3 lines of code. The next step is making this **zero-code** and **language-agnostic**:

- **Kudu/SCM integration**: The dashboard HTML and API could be served directly from the SCM site (similar to how we serve log streams, process explorer, etc. today). Agent telemetry written to `$HOME/LogFiles/` by any runtime — .NET, Python, Node.js, Java — would be picked up and displayed. The file-based JSONL approach we already built is runtime-agnostic by design.

- **Platform-level middleware**: For .NET, we could auto-inject the `IChatClient` middleware via a site extension or startup hook, requiring zero code changes. For Python/Node/Java, lightweight SDK shims that write to the same JSONL contract would enable the same dashboard without language-specific platform changes.

- **Portal blade**: A dedicated "Agent Observability" blade in the App Service portal resource, pulling from the same telemetry data. This would give operators and developers visibility without needing to open the app itself.

### 2. Bridge to the Foundry Control Plane

This directly addresses the Foundry integration challenge. Today, Foundry has no visibility into agents hosted on App Service — at best they might detect an app is "agentic" if we give them some sort of indication - this is a challenge we're currently investigating alongside the Functions team, but they can't see individual agents, their performance, or their topology - which is what Foundry Control Plane needs to provide value to customers.

This dashboard solves the **data collection problem** that's a prerequisite for Foundry integration:

- **Agent discovery**: The registry and auto-discovery mechanism identifies which agents exist, their names, types, and descriptions. This is exactly the metadata Foundry would need to represent App Service agents in their control plane.

- **Telemetry contract**: The JSONL file format and JSON API endpoints define a structured telemetry contract (agent name, invocations, latency, tokens, errors, topology). Foundry could consume this data — either by reading the file directly (for co-located scenarios) or by calling the API endpoints.

- **Topology as metadata**: The workflow phase definitions (which agents exist, how they connect, what order they execute in) could be surfaced to Foundry as agent metadata, giving them the structural understanding they're missing.

The path could look like:
1. **Now**: NuGet package for .NET customers (done, validated)
2. **Next**: Platform-native integration in Kudu/SCM (language-agnostic, zero-code)
3. **Then**: Foundry reads the standardized telemetry contract to surface App Service agents in their control plane — either via the JSON API or a shared telemetry format

This means App Service doesn't need to wait for Foundry to design their integration — we can define the telemetry contract now, ship customer value immediately, and hand Foundry a well-defined interface to consume when they're ready.

## Repo

[github.com/seligj95/app-service-hosted-agent-dashboard](https://github.com/seligj95/app-service-hosted-agent-dashboard)

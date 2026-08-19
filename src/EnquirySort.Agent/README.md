# EnquirySort Agent (LangGraph + Amazon Bedrock)

Python service that runs the AI triage workflow:

```text
classify → (FAQ) retrieve → apply response rules → draft
         → (team) route
         → ignore
```

The .NET API still owns IMAP/SMTP, SQL, and the admin UI. It calls this agent when `Ai:Provider` is `BedrockAgent`.

## Why LangGraph (not LangChain alone)

| Piece | Role |
|-------|------|
| **LangGraph** | Workflow graph, branching (FAQ vs department), future human-in-the-loop |
| **LangChain (`langchain-aws`)** | Bedrock chat model + prompt/message helpers |

## Setup

```bash
cd src/EnquirySort.Agent
python -m venv .venv
# Windows: .venv\Scripts\activate
source .venv/bin/activate
pip install -e ".[dev]"
cp .env.example .env
```

Configure AWS credentials (any of):

- `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` / `AWS_REGION`
- or an IAM role / instance profile
- or `aws configure`

Enable Bedrock model access in the AWS console for your region (e.g. Claude).

## Run

```bash
enquirysort-agent
# http://127.0.0.1:8090/health
# POST http://127.0.0.1:8090/triage
```

## Point the API at the agent

In `appsettings.Development.json`:

```json
"Ai": {
  "Provider": "BedrockAgent",
  "AgentBaseUrl": "http://127.0.0.1:8090"
}
```

Keep `"Provider": "OpenRouter"` to use the existing OpenRouter path.

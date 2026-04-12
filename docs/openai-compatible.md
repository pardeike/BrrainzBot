# OpenAI-Compatible Endpoints

BrrainzBot talks to an OpenAI-style API for the verification decision. The bot sends a short prompt and expects a strict JSON response.

## What You Need

- a base URL, for example `https://api.openai.com/v1`
- a model name
- an API key

## Safety Rules

BrrainzBot is intentionally conservative:

- it prefers HTTPS endpoints
- it keeps secrets in a separate local file
- it never checks for new versions in the background
- it only contacts GitHub for updates when you explicitly run `self-update`

## When to Allow Insecure Local Endpoints

Only do this if:

- the endpoint is on your own machine or private network
- you understand that the connection is not using standard HTTPS protection

For normal hosted endpoints, keep HTTPS required.

## Keeping Costs and Latency Under Control

Use a fast small model first. The bot only needs a short judgement, not a long essay.

Good characteristics:

- fast
- cheap
- reliable JSON output
- good enough classification for obvious humans versus obvious junk

## What the Bot Sends

The bot sends:

- your guild-specific topic prompt
- a short rules hint
- the user’s three answers
- the attempt number

It does not need a long conversation history for v1.

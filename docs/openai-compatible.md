# OpenAI-Compatible Endpoints

BrrainzBot asks an OpenAI-style API for one short decision during onboarding.

It sends a short prompt and expects strict JSON back.

## What You Need

- a base URL such as `https://api.openai.com/v1`
- a model name
- an API key

## Recommended Starting Point

Start small and fast.

A good default is:

- model: `gpt-5.4-nano`

The bot needs a short judgement, not a long answer.

## Safety Rules

BrrainzBot is conservative on purpose:

- it prefers HTTPS
- it stores secrets in a separate local file
- it does not check GitHub for updates unless you run `self-update`

## When an Insecure Local Endpoint Is Fine

Only allow that if:

- the endpoint is on your own machine or private network
- you understand that the connection is not protected by normal HTTPS

For normal hosted endpoints, keep HTTPS required.

## What the Bot Sends

The bot sends:

- your server topic prompt
- a short rules hint
- the user’s three answers
- the attempt number

It does not need a long conversation history.

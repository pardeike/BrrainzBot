# Discord Setup

This guide is the whole path from zero to a ready server.

## Before You Start

Check these first:

- you are logged into the right Discord account
- that account has permission to invite bots to the target server
- your Discord account email is verified
- you know which server you want to test first

## 1. Open the Discord Developer Portal

Open:

`https://discord.com/developers/applications`

Then click **New Application**.

The application name does not have to be perfect. You can change it later.

## 2. Create the Bot User

Inside the application:

1. Open the **Bot** tab.
2. If Discord has not created a bot user yet, create one there.
3. Copy the bot token for `brrainzbot setup`.

## 3. Turn On the Required Intents

Still in the **Bot** tab, turn on:

- `Server Members Intent`
- `Message Content Intent`

## 4. Ignore the Fields You Do Not Need

For this self-hosted setup, you can ignore these unless you already know you need them:

- `Interactions Endpoint URL`
- `Linked Roles Verification URL`
- `Terms of Service URL`
- `Privacy Policy URL`

## 5. Decide on `Public Bot`

For a normal self-hosted install, keeping `Public Bot` on is fine.

What it means:

- `on`: other people could invite this bot if they had the invite link and the right Discord permissions
- `off`: only you or your team can invite it

This does **not** mean other people can run code on your machine.

## 6. Generate the Invite Link

Open **OAuth2** → **URL Generator**.

Shortcut:

```bash
./brrainzbot invite-url --client-id <appId>
```

If you already ran `setup`, this also works and will resolve the app ID from the saved bot token:

```bash
./brrainzbot invite-url
```

Under **Scopes**, select:

- `bot`

Keep the scope simple. You do not need extra scopes for the current welcome flow.

Under **Bot Permissions**, select all seven required permissions:

- `View Channels`
- `Read Message History`
- `Send Messages`
- `Manage Messages`
- `Manage Roles`
- `Manage Channels`
- `Kick Members`

If you use the portal generator, copy the generated URL at the bottom.

## 7. Invite the Bot

Open the invite URL in your browser.

Do this once for each server where you want to use the bot:

1. choose the server
2. authorize the bot
3. return to Discord

## 8. Create the Roles

In your Discord server, open:

**Server name** → **Server Settings** → **Roles**

Create these roles:

- `MEMBER`

Recommended model:

- `@everyone`: can look around, but does not grant normal posting
- `MEMBER`: grants normal participation

If your server already uses `@everyone` for normal posting, BrrainzBot can help you move to a real `MEMBER` role:

```bash
./brrainzbot create-member <serverId>
./brrainzbot set-members <serverId>
```

## 9. Create `#welcome`

Create a channel called `#welcome`.

You do **not** need to post anything there yourself. BrrainzBot will place the persistent welcome post.

Recommended `MEMBER` model:

- `@everyone`: can view `#welcome`
- `MEMBER`: cannot view `#welcome`

## 10. Set Public Channel Permissions

### Recommended `MEMBER` model

For the public channels you want newcomers to see:

- `@everyone`: can view
- `@everyone`: cannot post
- `MEMBER`: can post

This is the safer model. New users never get normal posting access until approval.

## 11. Move the Bot Role Above `MEMBER`

Open the server role list and move the bot role above:

- `MEMBER`

If the bot role is too low, the bot cannot move users between roles.

## 12. Gather the IDs

Turn on **Developer Mode** in Discord first:

**User Settings** → **Advanced** → **Developer Mode**

Then copy these IDs:

- server ID
- `#welcome` channel ID
- spam honeypot channel ID if you use spam cleanup
- `MEMBER` role ID
- your own Discord user ID

## 13. Run Setup

Back in the terminal:

```bash
./brrainzbot setup
```

## 14. Run Doctor

```bash
./brrainzbot doctor
```

Fix anything it reports before you turn the server on.

## 15. Enable the Server

```bash
./brrainzbot enable <serverId>
```

## 16. Start the Bot

```bash
./brrainzbot run
```

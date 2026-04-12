# Discord Setup

This is the part most guides make unnecessarily confusing. Keep the model simple:

- `NEW` means "can look, cannot participate yet"
- `MEMBER` means "normal access"
- `#welcome` is for starting verification

## 1. Create the Bot Application

In the Discord Developer Portal:

1. Create a new application.
2. Add a bot user.
3. Copy the bot token for `brrainzbot setup`.
4. Enable the privileged intents your bot needs:
   - `Server Members Intent`
   - `Message Content Intent`

## 2. Invite the Bot

Invite the bot with enough permissions to:

- view channels
- manage roles
- manage messages
- kick members
- send messages
- create DMs

Keep the bot role above `NEW` and `MEMBER` in the Discord role order, otherwise it cannot move people between those roles.

## 3. Create the Roles

Create:

- `NEW`
- `MEMBER`

Recommended role model:

- `@everyone`: read access to the channels you want newcomers to browse, but no send permission in the public discussion channels
- `NEW`: marker role for jailed users
- `MEMBER`: the role that actually grants normal send/participation access

## 4. Create `#welcome`

`#welcome` should:

- be visible to `@everyone` and `NEW`
- be hidden from `MEMBER`
- not be used as a normal chat room

The bot will place one persistent welcome message there and handle the rest with buttons, modals, and ephemeral replies.

## 5. Make Public Channels Read-Only for Newcomers

For the public channels you want newcomers to browse:

- allow view access to `@everyone`
- deny or leave disabled message sending for `@everyone`
- grant normal send access to `MEMBER`

That gives new users a good first impression without letting drive-by spam hit the visible channels immediately.

## 6. Gather the IDs

You will need:

- guild ID
- welcome channel ID
- `NEW` role ID
- `MEMBER` role ID
- your own Discord user ID for uncertain-case DMs

To get them:

1. Enable Developer Mode in Discord.
2. Right-click the server, roles, and channel.
3. Use **Copy ID**.

# Discord Setup

This is the part most guides make unnecessarily confusing. Keep the model simple:

- `NEW` means "can look, cannot participate yet"
- `MEMBER` means "normal access"
- `#welcome` is for starting verification

There are two valid setups:

- standard setup: `MEMBER` is a real role that grants normal participation
- simple setup: use `@everyone` as the member state by entering the guild ID where the bot asks for the member role ID

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

If you use the simple setup with `@everyone`, the bot still needs to sit above `NEW`.

## 3. Create the Roles

Create:

- `NEW`
- `MEMBER`

Recommended role model:

- `@everyone`: read access to the channels you want newcomers to browse, but no send permission in the public discussion channels
- `NEW`: marker role for jailed users
- `MEMBER`: the role that actually grants normal send/participation access

Simpler role model for small servers:

- `@everyone`: normal member state
- `NEW`: marker role for jailed users and the role that denies posting in the public channels until approval
- no separate `MEMBER` role

## 4. Create `#welcome`

Standard setup:

- `#welcome` is visible to `@everyone` and `NEW`
- `#welcome` is hidden from `MEMBER`

Simple setup with `@everyone`:

- `#welcome` is hidden from `@everyone`
- `#welcome` is visible to `NEW`
- approval hides it by removing `NEW`

In both setups, `#welcome` should not be used as a normal chat room.

The bot will place one persistent welcome message there and handle the rest with buttons, modals, and ephemeral replies.

## 5. Make Public Channels Read-Only for Newcomers

For the public channels you want newcomers to browse:

- allow view access to `@everyone`
- deny or leave disabled message sending for `@everyone`
- grant normal send access to `MEMBER`

That gives new users a good first impression without letting drive-by spam hit the visible channels immediately.

If you use the simple setup with `@everyone`:

- allow normal send access to `@everyone`
- deny send access to `NEW`
- let approval work by removing `NEW`

## 6. Gather the IDs

You will need:

- guild ID
- welcome channel ID
- `NEW` role ID
- `MEMBER` role ID, or the guild ID if you want to use `@everyone`
- your own Discord user ID for uncertain-case DMs

To get them:

1. Enable Developer Mode in Discord.
2. Right-click the server, roles, and channel.
3. Use **Copy ID**.

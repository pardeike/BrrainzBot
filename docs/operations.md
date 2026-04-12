# Operations

## Start the Bot

```bash
./brrainzbot run
```

If the bot is disabled in config, this starts an idle process that does not connect to Discord.

## Revalidate After Changes

Any time you change roles, channels, or tokens:

```bash
./brrainzbot doctor
```

## See the Effective Configuration

```bash
./brrainzbot print-config
```

Secrets are redacted in the output.

## Update the Installed Binary

```bash
./brrainzbot self-update
```

The updater is manual on purpose.

Before it replaces the binary, it shows:

- your current version
- the candidate release
- release notes
- the asset it plans to install

## Logs

The bot writes local JSONL audit logs. These are useful for:

- uncertain verification cases
- debugging technical failures
- reviewing stale-user kicks
- inspecting spam-trigger events

## Common Problems

### The bot cannot move users between roles

Check the Discord role order. The bot role must sit above both `NEW` and `MEMBER`.

If you use `@everyone` as the member state, the bot still needs to sit above `NEW`.

### The bot cannot see the welcome channel

Run `doctor` and verify the welcome channel ID and permissions.

### Self-update does nothing

Check that:

- the configured GitHub repository is correct
- the release exists
- there is a published asset for your platform

### New users can still talk in public channels

That is a Discord permission layout problem, not a bot bug.

Use one of these models:

- separate `MEMBER` role: normal send access comes from `MEMBER`, not `@everyone`
- `@everyone` member state: normal send access comes from `@everyone`, and `NEW` explicitly denies sending until approval

---
title: Friendly Discord onboarding and spam defense
hide:
  - navigation
  - toc
---

<section class="hero-shell">
  <div class="hero-grid">
    <div class="hero-copy reveal">
      <div class="hero-kicker"><span></span>Self-hosted, friendly, and built to stay out of the way</div>
      <h1>Make your server feel welcoming<br>without making it easy to abuse.</h1>
      <p class="lede">
        BrrainzBot gives new people a calm first impression, keeps verification out of public chat, and makes drive-by spam feel expensive.
        It is built for communities that want real users to feel seen and low-effort abuse to feel pointless.
      </p>
      <div class="hero-actions">
        <a class="bb-button primary" href="installation/">Get started</a>
        <a class="bb-button secondary" href="discord-setup/">See the Discord setup</a>
      </div>
      <div class="hero-points">
        <div class="hero-point">
          <strong>No public verification clutter</strong>
          Buttons, modals, and ephemeral replies do the talking.
        </div>
        <div class="hero-point">
          <strong>Read-only first impression</strong>
          New people can look around before they can post.
        </div>
        <div class="hero-point">
          <strong>Manual updates only</strong>
          No background version checks, no quiet surprises.
        </div>
      </div>
    </div>
    <div class="hero-visual reveal">
      <div class="hero-card">
        <img src="assets/illustrations/quiet-lobby.svg" alt="A bright, calm illustration of a friendly server lobby with a clear welcome path.">
      </div>
      <div class="hero-mini">
        <strong>Friendly outside</strong>
        Clear setup, clear copy, and a verification flow that does not embarrass your server.
      </div>
    </div>
  </div>
</section>

<section class="bb-section reveal">
  <div class="section-heading">
    <span class="eyebrow">What it does</span>
    <h2>Two focused jobs, one calm experience.</h2>
    <p>
      BrrainzBot combines a user-friendly welcome flow with practical spam defense. It is meant to feel polished for admins and natural for normal people joining a server for the first time.
    </p>
  </div>
  <div class="feature-grid">
    <article class="bb-card">
      <div class="icon-badge blue">✦</div>
      <h3>Onboarding that feels private</h3>
      <p>
        New users start in <code>NEW</code>, can browse public channels in read-only mode, and verify through one tidy welcome panel instead of leaving awkward chat logs behind.
      </p>
      <ul>
        <li>Persistent welcome post</li>
        <li>Button + modal flow</li>
        <li>Ephemeral replies only</li>
      </ul>
    </article>
    <article class="bb-card">
      <div class="icon-badge teal">✓</div>
      <h3>Spam defense that stays practical</h3>
      <p>
        Honeypot triggers and cross-channel duplicate detection give you a strong first line of defense against obvious link spam and repeated drive-by posts.
      </p>
      <ul>
        <li>Honeypot trigger cleanup</li>
        <li>Duplicate post detection</li>
        <li>Per-guild configuration</li>
      </ul>
    </article>
    <article class="bb-card">
      <div class="icon-badge coral">↺</div>
      <h3>Admin experience that respects your time</h3>
      <p>
        Setup is guided, reconfiguration is built in, diagnostics are plain-language, and self-update only happens when you ask for it.
      </p>
      <ul>
        <li><code>setup</code> and <code>reconfigure</code></li>
        <li><code>doctor</code> validation</li>
        <li>Manual GitHub-backed updates</li>
      </ul>
    </article>
  </div>
</section>

<section class="bb-section story-grid reveal">
  <div class="bb-illustration-card">
    <img src="assets/illustrations/welcome-flow.svg" alt="A soft, editorial illustration of a quiet welcome flow with prompts and private replies.">
  </div>
  <div class="story-copy">
    <div class="section-heading">
      <span class="eyebrow">Why it feels different</span>
      <h2>It is designed around first impressions on both sides.</h2>
      <p>
        Most moderation bots optimize for control panels and configuration depth first. BrrainzBot starts from the moment somebody arrives in your server and asks a simpler question:
        what should this feel like for a real human?
      </p>
    </div>
    <div class="split-grid">
      <div class="split-copy">
        <h3>For the new user</h3>
        <p>
          They see a real server, not an empty waiting room. They get a short, clear interaction instead of a wall of rules and a public interrogation.
        </p>
      </div>
      <div class="split-copy">
        <h3>For the admin</h3>
        <p>
          You get a system that is explainable, local-first, and repairable. The bot helps you stay in control without forcing you into a dashboard or a SaaS dependency.
        </p>
      </div>
    </div>
  </div>
</section>

<section class="bb-section reveal">
  <div class="section-heading">
    <span class="eyebrow">How it works</span>
    <h2>A setup path that stays simple even when you forget the details.</h2>
    <p>
      You do not need to remember the whole Discord and bot-registration dance from memory. The flow is meant to be repeatable, clear, and forgiving.
    </p>
  </div>
  <div class="steps-grid">
    <article class="bb-card step-card">
      <div class="step-number">1</div>
      <h3>Prepare the server</h3>
      <p>Create <code>NEW</code>, <code>MEMBER</code>, and <code>#welcome</code>. Let new users browse, but grant normal participation through <code>MEMBER</code>.</p>
      <a href="discord-setup/">Read the Discord setup guide →</a>
    </article>
    <article class="bb-card step-card">
      <div class="step-number">2</div>
      <h3>Run the wizard</h3>
      <p>Use <code>brrainzbot setup</code> to enter the bot token, the OpenAI-compatible endpoint, and your guild IDs. Use <code>reconfigure</code> later when things change.</p>
      <a href="installation/">See installation →</a>
    </article>
    <article class="bb-card step-card">
      <div class="step-number">3</div>
      <h3>Validate before go-live</h3>
      <p>Run <code>brrainzbot doctor</code> to catch missing IDs, broken permissions, invalid tokens, and endpoint mistakes before your community feels them.</p>
      <a href="operations/">See operations →</a>
    </article>
  </div>
</section>

<section class="bb-section story-grid reveal">
  <div class="story-copy">
    <div class="section-heading">
      <span class="eyebrow">For self-hosters</span>
      <h2>Transparent by default.</h2>
      <p>
        The bot keeps its state locally, logs locally, and only checks GitHub for updates when you explicitly run the update command. If you need to change your setup, you do not start over. You repair it in place.
      </p>
    </div>
    <div class="split-grid">
      <div class="split-copy">
        <h3>Safe endpoint setup</h3>
        <p>OpenAI-compatible endpoints are configured directly and documented in plain language, including the secure default assumptions.</p>
      </div>
      <div class="split-copy">
        <h3>Mobile-friendly release flow</h3>
        <p>GitHub Actions handles release packaging and documentation deployment so simple fixes can be validated and promoted without needing a full desktop workflow.</p>
      </div>
    </div>
  </div>
  <div class="bb-illustration-card">
    <img src="assets/illustrations/calm-ops.svg" alt="A bright operations illustration with a terminal, release checks, and documentation cards.">
  </div>
</section>

<section class="cta-panel reveal">
  <div class="section-heading">
    <span class="eyebrow">Start here</span>
    <h2>Set up the bot once, then let it stay boring in the best possible way.</h2>
    <p>
      Read the Discord setup guide if you are starting from scratch. If you already know your IDs and have a bot token ready, jump straight to installation.
    </p>
  </div>
  <div class="hero-actions">
    <a class="bb-button primary" href="installation/">Install BrrainzBot</a>
    <a class="bb-button secondary" href="configuration/">Read the configuration guide</a>
  </div>
</section>

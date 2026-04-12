---
title: Friendly Discord onboarding and spam defense
hide:
  - navigation
  - toc
---

<section class="hero-shell">
  <div class="hero-grid">
    <div class="hero-copy reveal">
      <div class="hero-kicker"><span></span>Self-hosted. Clear. Quiet.</div>
      <h1>Let people in.<br>Keep spam out.</h1>
      <p class="lede">
        BrrainzBot gives new users a readable first view, keeps verification out of public chat, and removes common spam patterns.
        It is for Discord servers that want less noise and less setup friction.
      </p>
      <div class="hero-actions">
        <a class="bb-button primary" href="installation/">Install</a>
        <a class="bb-button secondary" href="discord-setup/">Discord setup</a>
      </div>
      <div class="hero-points">
        <div class="hero-point">
          <strong>Private verification</strong>
          Buttons, modals, and ephemeral replies. No public welcome mess.
        </div>
        <div class="hero-point">
          <strong>Read-only preview</strong>
          New users can look around before they can post.
        </div>
        <div class="hero-point">
          <strong>Manual updates</strong>
          No background version checks. No silent changes.
        </div>
      </div>
    </div>
    <div class="hero-visual reveal">
      <div class="hero-card">
        <img src="assets/illustrations/quiet-lobby.svg" alt="A bright, calm illustration of a friendly server lobby with a clear welcome path.">
      </div>
      <div class="hero-mini">
        <strong>Good defaults</strong>
        Clear setup. Clear copy. Clean behavior in Discord.
      </div>
    </div>
  </div>
</section>

<section class="bb-section reveal">
  <div class="section-heading">
    <span class="eyebrow">What it does</span>
    <h2>One bot. Two jobs.</h2>
    <p>
      It handles entry and cleanup. New users verify in <code>#welcome</code>. SpamGuard removes honeypot and duplicate spam.
    </p>
  </div>
  <div class="feature-grid">
    <article class="bb-card">
      <div class="icon-badge blue">✦</div>
      <h3>Quiet onboarding</h3>
      <p>
        New users start in <code>NEW</code>, can browse public channels in read-only mode, and verify through one welcome panel.
      </p>
      <ul>
        <li>Persistent welcome post</li>
        <li>Button + modal flow</li>
        <li>Ephemeral replies only</li>
      </ul>
    </article>
    <article class="bb-card">
      <div class="icon-badge teal">✓</div>
      <h3>Practical spam defense</h3>
      <p>
        Honeypot triggers and cross-channel duplicate detection catch the common low-effort cases early.
      </p>
      <ul>
        <li>Honeypot trigger cleanup</li>
        <li>Duplicate post detection</li>
        <li>Per-guild configuration</li>
      </ul>
    </article>
    <article class="bb-card">
      <div class="icon-badge coral">↺</div>
      <h3>Simple operations</h3>
      <p>
        Setup is guided. Reconfiguration is built in. Diagnostics are plain. Updates stay manual.
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
      <span class="eyebrow">Why it is built this way</span>
      <h2>Start with the join moment.</h2>
      <p>
        Most bots start with commands and dashboards. This one starts with arrival. A real user should understand the server fast. A spammer should lose time fast.
      </p>
    </div>
    <div class="split-grid">
      <div class="split-copy">
        <h3>For the new user</h3>
        <p>
          They see a real server, not an empty waiting room. They get a short flow, not a public interrogation.
        </p>
      </div>
      <div class="split-copy">
        <h3>For the admin</h3>
        <p>
          You get local state, clear rules, and a setup you can repair without a dashboard.
        </p>
      </div>
    </div>
  </div>
</section>

<section class="bb-section reveal">
  <div class="section-heading">
    <span class="eyebrow">How it works</span>
    <h2>Setup in three steps.</h2>
    <p>
      You do not need to remember the Discord maze. Follow the same short path each time.
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
      <p>Use <code>brrainzbot setup</code> for the bot token, the AI endpoint, and your guild IDs. Use <code>reconfigure</code> later.</p>
      <a href="installation/">See installation →</a>
    </article>
    <article class="bb-card step-card">
      <div class="step-number">3</div>
      <h3>Validate before go-live</h3>
      <p>Run <code>brrainzbot doctor</code> to catch missing IDs, broken permissions, invalid tokens, and endpoint mistakes.</p>
      <a href="operations/">See operations →</a>
    </article>
  </div>
</section>

<section class="bb-section story-grid reveal">
  <div class="story-copy">
    <div class="section-heading">
      <span class="eyebrow">For self-hosters</span>
      <h2>Local state. Explicit updates.</h2>
      <p>
        Config, state, and logs stay local. The bot checks GitHub only when you run the update command. If your setup changes, repair it in place.
      </p>
    </div>
    <div class="split-grid">
      <div class="split-copy">
        <h3>Safe endpoint setup</h3>
        <p>OpenAI-compatible endpoints are configured directly. The docs keep the safe defaults clear.</p>
      </div>
      <div class="split-copy">
        <h3>Phone-friendly release flow</h3>
        <p>GitHub Actions handles builds, docs, and release packaging, so small fixes can move without a full desktop workflow.</p>
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
    <h2>Start with setup. Then leave it alone.</h2>
    <p>
      If you are starting from zero, read the Discord setup guide. If you already have the IDs and token, go straight to installation.
    </p>
  </div>
  <div class="hero-actions">
    <a class="bb-button primary" href="installation/">Install BrrainzBot</a>
    <a class="bb-button secondary" href="configuration/">Read the configuration guide</a>
  </div>
</section>

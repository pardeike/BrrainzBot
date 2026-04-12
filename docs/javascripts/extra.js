document.addEventListener("DOMContentLoaded", () => {
  const observer = new IntersectionObserver(
    entries => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
        }
      }
    },
    {
      threshold: 0.14,
      rootMargin: "0px 0px -10% 0px"
    }
  );

  document.querySelectorAll(".reveal").forEach(element => observer.observe(element));
});

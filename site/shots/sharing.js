// The website's sharing shot (img/sharing.png), in the real window. Run by sharing.ps1 with EQLPATHFINDER_SELFTEST=mock.
// It only navigates and clicks, the way a player would: every word and number on the shot is the app's own answer,
// from the demo logs and the sample pool. Ask -> Mote farm at my level -> 50, in the scope SCOPE ("patch", the page's
// own default, or "all"), which sharing.ps1 sets on the line it adds above this file.
(function () {
  const $ = s => document.querySelector(s);
  const click = s => [() => $(s), () => $(s).click()];
  const steps = [
    [() => typeof data !== "undefined" && data?.reinc, () => show("ask")],
    click('[data-ask-q="farm"]'),
    click('[data-ask-at="50"]'),
    click(`[data-ask-scope="${SCOPE}"]`),
  ];
  // Each step waits for its button, clicks it, then gives the answer 3 seconds to arrive before the next.
  (function next(i) {
    if (i >= steps.length) return;
    const [ready, act] = steps[i];
    if (!ready()) return setTimeout(() => next(i), 500);
    act();
    setTimeout(() => next(i + 1), 3000);
  })(0);
})();

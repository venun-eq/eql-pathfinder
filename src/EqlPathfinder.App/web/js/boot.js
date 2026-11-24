"use strict";
// Last: everything is defined now, so the first render and the resize redraws can run.
new ResizeObserver(() => { if (page === "home") drawRace(); else if (page === "relevels") drawRelevels(); else if (page === "loot") drawLootChart(); }).observe($("#main"));
show(page, true);

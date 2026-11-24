# The fight meter's Crawl starts again with each run

**Why:** Two Kedge Keep D4 crawls run back to back read as one 30-minute crawl on the fight meter, while the relevel
overlay showed the 5-minute run (feedback #10, 0.10.10). The meter took going back into the same zone within 10
minutes as a return after a death, even when the crawl was done and a new instance had been made.
**Players:** everyone who farms one zone run after run. Worst at D4, where runs are short and repeated.

## Done when
- [x] A crawl done, out to the public zone, a new instance and in again is a new Crawl on the meter - `BackToBackCrawlsOfOneZoneAreTwoCrawls`
- [x] Either sign alone (the crawl done, or a new instance made) starts a new Crawl, as the crawl tracker has it - `ACrawlDoneOrANewInstanceEndsTheCrawlEvenWithoutTheOther`
- [x] A D0 crawl done, or a new D0 instance made, isn't resumed under the plain zone name - `AD0CrawlDoneIsntResumedByThePlainName`
- [x] A death or an errand inside 10 minutes, with neither sign, is still the same Crawl - `ASessionStartsAfterThirtyMinutesAwayAndAStintWithTheZone`, `AD0InstanceIsACrawlForTheMeterToo`

## Edge cases
- **Same name, different thing:** two instances of one zone, minutes apart. The instance number in "creating
  instance Befallen 776" isn't compared; the creation line itself is the sign, as in `Tracker.OnZone`.
- **D0:** a new instance or a done crawl also stops a plain-named zone being taken for the last D0 instance.
- **Starting and stopping:** the three new flags are saved in the meter's checkpoint with the rest of `FightContext`.
  A checkpoint from before this reads them as false: the stint open at the update behaves as it did before.
- **Old versions in the field:** nothing is sent; an older app reading a newer checkpoint ignores the extra fields.

## Not doing
- Repairing stints already stored. Merged stints only feed the meter's "usual" (by stay), and a re-read of every
  fight costs players minutes for little.

## Where
- `src/EqlPathfinder.Engine/Combat/FightTracker.cs`: `FightContext` (`Completed`, `NewInstanceSinceEntry`,
  `LeftCompleted`), `Apply`, `OnZone`

## Reviews
- architect: the fix mirrors the crawl tracker's done and new-instance rules, and the checkpoint stays compatible both ways; nothing blocks. One should fix, no test of the D0 plain-name rule: added `AD0CrawlDoneIsntResumedByThePlainName`, which fails on main's tracker.

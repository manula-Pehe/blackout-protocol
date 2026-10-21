using System.Collections.Generic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Sentinel.Tests
{
    public sealed class SentinelHsmTests
    {
        [Test]
        public void TransitionsThroughAlertTrackSearchReturnAndPatrol()
        {
            var hsm = new SentinelHsm();
            var transitions = new List<string>();
            hsm.StateChanged += (from, to) => transitions.Add($"{from}>{to}");

            Assert.That(hsm.State, Is.EqualTo(SentinelState.Patrol));
            hsm.Tick(new SentinelHsmInput(true, false, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Alert));
            Assert.That(hsm.ParentState, Is.EqualTo(SentinelStateGroup.Contact));
            hsm.Tick(new SentinelHsmInput(true, true, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Track));
            hsm.Tick(new SentinelHsmInput(false, false, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Search));
            hsm.Tick(new SentinelHsmInput(false, false, true, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Return));
            Assert.That(hsm.ParentState, Is.EqualTo(SentinelStateGroup.Routine));
            hsm.Tick(new SentinelHsmInput(false, false, false, true), 0.1f);

            Assert.That(hsm.State, Is.EqualTo(SentinelState.Patrol));
            Assert.That(transitions, Is.EqualTo(new[] { "Patrol>Alert", "Alert>Track", "Track>Search", "Search>Return", "Return>Patrol" }));
            Assert.That(hsm.StateElapsed, Is.Zero);
        }

        [Test]
        public void ReacquiringPlayerDuringSearchReturnsToAlert()
        {
            var hsm = new SentinelHsm();
            hsm.Tick(new SentinelHsmInput(true, false, false, false), 0.1f);
            hsm.Tick(new SentinelHsmInput(false, true, false, false), 0.1f);
            hsm.Tick(new SentinelHsmInput(true, false, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Alert));
        }

        [Test]
        public void ExitsActiveChildWhenContactParentExits()
        {
            var hsm = new SentinelHsm();
            SentinelState exited = SentinelState.Patrol;
            hsm.StateExiting += state => exited = state;
            hsm.Tick(new SentinelHsmInput(true, false, false, false), 0.1f);
            hsm.Tick(new SentinelHsmInput(false, true, false, false), 0.1f);
            hsm.Tick(new SentinelHsmInput(false, false, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Search));
            Assert.That(hsm.ParentState, Is.EqualTo(SentinelStateGroup.Contact));

            hsm.Tick(new SentinelHsmInput(false, false, true, false), 0.1f);

            Assert.That(exited, Is.EqualTo(SentinelState.Search));
            Assert.That(hsm.ParentState, Is.EqualTo(SentinelStateGroup.Routine));
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Return));
        }

        [Test]
        public void PlayerDeathReturnsSentinelDirectlyToPatrol()
        {
            var hsm = new SentinelHsm();
            hsm.Tick(new SentinelHsmInput(true, false, false, false), 0.1f);
            hsm.Tick(new SentinelHsmInput(true, true, false, false), 0.1f);
            Assert.That(hsm.State, Is.EqualTo(SentinelState.Track));

            hsm.Tick(new SentinelHsmInput(false, false, false, false, playerDead: true), 0.1f);

            Assert.That(hsm.State, Is.EqualTo(SentinelState.Patrol));
            Assert.That(hsm.ParentState, Is.EqualTo(SentinelStateGroup.Routine));
        }
    }
}

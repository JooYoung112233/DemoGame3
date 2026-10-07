using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// v058 approved exterior: structure, usable anchors, clear Core routes and preserved gameplay constants.
    /// Runtime JSON contacts, rendered silhouettes and actual Physics2D movement are verified separately.
    /// </summary>
    public sealed class TownLayoutTests
    {
        const float PlayerRadius = 0.4f;
        static readonly string[] Npcs = { "npc.gate", "npc.smith", "npc.trainer" };
        static IEnumerable<TownVec> Homes => Npcs.Select(TownLayout.NpcHome);

        static TownBox WithPlayerMargin(TownBox box) => new TownBox(
            box.Name + " + player radius", box.XMin - PlayerRadius, box.YMin - PlayerRadius,
            box.XMax + PlayerRadius, box.YMax + PlayerRadius);

        static bool SamePoint(TownVec a, TownVec b) => TownVec.Distance(a, b) < 0.0001f;

        [Test]
        public void NpcSpacing()
        {
            foreach (var id in Npcs)
                foreach (var facility in TownLayout.Facilities)
                    Assert.GreaterOrEqual(TownVec.Distance(TownLayout.NpcHome(id), facility.Pos),
                        TownLayout.NpcRadius + facility.Radius, $"{id} – {facility.Id} F radii overlap");

            var homes = Homes.ToList();
            for (int i = 0; i < homes.Count; i++)
                for (int j = i + 1; j < homes.Count; j++)
                    Assert.GreaterOrEqual(TownVec.Distance(homes[i], homes[j]), TownLayout.NpcRadius * 2f,
                        $"{Npcs[i]} – {Npcs[j]} NPC radii overlap");
        }

        [Test]
        public void FacilitySpotsDoNotOverlap()
        {
            var facilities = TownLayout.Facilities;
            Assert.AreEqual(5, facilities.Length);
            Assert.AreEqual(facilities.Length, facilities.Select(f => f.Id).Distinct().Count());
            for (int i = 0; i < facilities.Length; i++)
                for (int j = i + 1; j < facilities.Length; j++)
                    Assert.GreaterOrEqual(TownVec.Distance(facilities[i].Pos, facilities[j].Pos),
                        facilities[i].Radius + facilities[j].Radius, $"{facilities[i].Id} – {facilities[j].Id}");

            Assert.AreEqual(1.6f, TownLayout.NpcRadius);
            Assert.AreEqual(1.4f, TownLayout.FacilityRadius);
            Assert.AreEqual(1.2f, TownLayout.ObjectRadius);
            foreach (var facility in new[] { TownLayout.Gate, TownLayout.Anvil, TownLayout.Board })
                Assert.AreEqual(TownLayout.FacilityRadius, facility.Radius, facility.Id);
            foreach (var facility in new[] { TownLayout.Shutter, TownLayout.Cart })
                Assert.AreEqual(TownLayout.ObjectRadius, facility.Radius, facility.Id);
        }

        [Test]
        public void SpotsInsideWalkAndOutsideSolids()
        {
            var solids = TownLayout.Solids();
            var points = new List<(string name, TownVec pos)>();
            foreach (var facility in TownLayout.Facilities) points.Add((facility.Id, facility.Pos));
            foreach (var id in Npcs) points.Add((id, TownLayout.NpcHome(id)));
            points.Add(("return", TownLayout.ReturnPoint));
            points.Add(("new play", TownLayout.NewPlayStart));
            foreach (var point in points)
            {
                Assert.IsTrue(TownLayout.Walk.Contains(point.pos), point.name + " is outside Walk");
                foreach (var box in solids)
                    Assert.IsFalse(WithPlayerMargin(box).ContainsStrict(point.pos),
                        point.name + " has insufficient player clearance at " + box.Name);
            }
        }

        [Test]
        public void ApprovedBuildingCountAndRelativePositions()
        {
            var buildings = TownLayout.Buildings;
            Assert.AreEqual(7, buildings.Length, "seven buildings; mine is a separate facility");
            Assert.AreEqual(7, buildings.Select(b => b.Name).Distinct().Count());
            CollectionAssert.AreEquivalent(
                new[] { TownLayout.House.Name, TownLayout.Smithy.Name, TownLayout.Tavern.Name,
                    TownLayout.PlankHouse.Name, TownLayout.TarpHouse.Name, TownLayout.RuinBase.Name,
                    TownLayout.GuardPost.Name },
                buildings.Select(b => b.Name).ToArray());
            Assert.IsFalse(buildings.Any(b => b.Name == TownLayout.Winch.Name || b.Name == TownLayout.MineHole.Name));
            foreach (var building in buildings)
                Assert.IsTrue(TownLayout.Solids().Any(b => b.Name == building.Name), building.Name + " has no Core contact");

            var middle = TownLayout.Walk.Center;
            Assert.Less(TownLayout.House.Center.X, middle.X, "mayor is upper left");
            Assert.Greater(TownLayout.House.Center.Y, middle.Y);
            Assert.Greater(TownLayout.Smithy.Center.X, middle.X, "smith is upper right");
            Assert.Greater(TownLayout.Smithy.Center.Y, middle.Y);
            Assert.Greater(TownLayout.Tavern.Center.X, middle.X, "tavern is lower right");
            Assert.Less(TownLayout.Tavern.Center.Y, TownLayout.Smithy.YMin);

            foreach (var home in new[] { TownLayout.PlankHouse, TownLayout.TarpHouse, TownLayout.RuinBase })
                Assert.Less(home.Center.X, middle.X, home.Name + " is on the left");
            Assert.Less(TownLayout.PlankHouse.Center.X, TownLayout.TarpHouse.Center.X);
            Assert.Less(TownLayout.RuinBase.Center.Y, TownLayout.PlankHouse.Center.Y);
            Assert.Less(TownLayout.RuinBase.Center.Y, TownLayout.TarpHouse.Center.Y);
            Assert.Less(TownLayout.GuardPost.Center.X, TownLayout.NewPlayStart.X, "guard is left of entry");
            Assert.Less(TownLayout.GuardPost.Center.Y, TownLayout.RuinBase.Center.Y, "guard is at south entry");

            Assert.Greater(TownLayout.Winch.Center.X, TownLayout.House.Center.X);
            Assert.Less(TownLayout.Winch.Center.X, TownLayout.Smithy.Center.X);
            Assert.Greater(TownLayout.Winch.Center.Y, TownLayout.House.Center.Y);
            Assert.Greater(TownLayout.Winch.Center.Y, TownLayout.Smithy.Center.Y);
            Assert.IsTrue(TownLayout.Winch.Encloses(TownLayout.MineHole));
            Assert.GreaterOrEqual(TownLayout.MayorAnnex.XMin, TownLayout.House.XMax - 0.0001f);
            Assert.GreaterOrEqual(TownLayout.TavernAnnex.XMin, TownLayout.Tavern.XMax - 0.0001f);
            Assert.Greater(TownLayout.Anvil.Pos.X, TownLayout.Smithy.XMax, "anvil belongs to east outdoor yard");
            Assert.Greater(TownLayout.ForgeGlow.X, TownLayout.Smithy.XMax, "furnace belongs to east outdoor yard");
        }

        [Test]
        public void GroundsAndSolids()
        {
            Assert.IsTrue(TownLayout.Paint.Encloses(TownLayout.Walk));
            Assert.IsTrue(TownLayout.Paint.Contains(TownLayout.Pixel(0, 0)));
            Assert.IsTrue(TownLayout.Paint.Contains(TownLayout.Pixel(1536, 1024)));
            foreach (var box in TownLayout.Solids())
            {
                Assert.Greater(box.Width, 0f, box.Name);
                Assert.Greater(box.Height, 0f, box.Name);
                Assert.IsTrue(TownLayout.Paint.Encloses(box), box.Name + " exceeds painted bounds");
            }
            Assert.IsTrue(TownLayout.CartBox.Contains(TownLayout.CartPos));
            Assert.That(TownLayout.NewPlayStart.X, Is.InRange(TownLayout.SouthGapXMin, TownLayout.SouthGapXMax));
        }

        [Test]
        public void WalkTimesAndOpening()
        {
            Assert.AreEqual(5.2f, TownLayout.WalkSpeed, 0.0001f);
            Assert.LessOrEqual(TownLayout.WalkSeconds(TownLayout.ReturnPoint, TownLayout.Gate.Pos), 1f);
            Assert.AreEqual(1f, (TownLayout.OpeningLineY - TownLayout.NewPlayStart.Y) / TownLayout.WalkSpeed, 0.05f);
            Assert.AreEqual(0.8f, TownLayout.NewPlayFadeIn);
            Assert.AreEqual(0.5f, TownLayout.NewPlayBarkDelay);
            Assert.AreEqual(0.5f, TownLayout.ArrivalFadeIn);
            Assert.AreEqual(3, TownLayout.ChalkArrows.Length);
            foreach (var arrow in TownLayout.ChalkArrows)
            {
                Assert.IsTrue(TownLayout.Walk.Contains(arrow));
                Assert.Greater(arrow.Y, TownLayout.NewPlayStart.Y);
                Assert.Less(arrow.Y, TownLayout.OpeningLineY);
            }
        }

        [Test]
        public void ReportRouteVisitsResidentsAndAvoidsContacts()
        {
            var loop = TownLayout.ReportLoop();
            Assert.GreaterOrEqual(loop.Length, Npcs.Length + 2);
            Assert.IsTrue(SamePoint(loop[0], TownLayout.ReturnPoint));
            Assert.IsTrue(SamePoint(loop[loop.Length - 1], TownLayout.Gate.Pos));
            foreach (var home in Homes)
                Assert.IsTrue(loop.Any(p => SamePoint(p, home)), "report route omits a resident");
            var solids = TownLayout.Solids();
            for (int i = 0; i < loop.Length; i++)
            {
                Assert.IsTrue(TownLayout.Walk.Contains(loop[i]), "route waypoint " + i + " is outside Walk");
                foreach (var box in solids)
                {
                    var clearance = WithPlayerMargin(box);
                    Assert.IsFalse(clearance.ContainsStrict(loop[i]), "route waypoint " + i + " is inside " + box.Name);
                    if (i > 0)
                        Assert.IsFalse(clearance.SegmentHits(loop[i - 1], loop[i]), "report leg " + i + " clips " + box.Name);
                }
            }
            Assert.Greater(TownLayout.PathLength(loop), 0f);
            // The approved larger village supersedes the rejected layout's 28.6-unit / 6-second circuit.
            // Runtime JSON contacts and real Physics2D traversal are deliberately not inferred from this Core route.
        }

        [Test]
        public void SegmentHitsBox()
        {
            var box = new TownBox("box", 0, 0, 2, 2);
            Assert.IsTrue(box.SegmentHits(new TownVec(-1, 1), new TownVec(3, 1)));
            Assert.IsFalse(box.SegmentHits(new TownVec(-1, 3), new TownVec(3, 3)));
            Assert.IsFalse(box.SegmentHits(new TownVec(-1, 2), new TownVec(3, 2)), "touching an edge is not entering");
            Assert.IsTrue(box.SegmentHits(new TownVec(1, 1), new TownVec(1, 1.5f)));
        }

        [TestCase(16f / 9f)]
        [TestCase(21f / 9f)]
        [TestCase(4f / 3f)]
        public void CameraStaysInsidePaint(float aspect)
        {
            var bounds = TownLayout.CameraCenterBounds(aspect);
            float halfH = TownLayout.CameraSize, halfW = TownLayout.CameraSize * aspect;
            foreach (var point in new[] { new TownVec(bounds.XMin, bounds.YMin), new TownVec(bounds.XMax, bounds.YMax),
                new TownVec(bounds.XMin, bounds.YMax), new TownVec(bounds.XMax, bounds.YMin) })
            {
                var view = new TownBox("view", point.X - halfW, point.Y - halfH, point.X + halfW, point.Y + halfH);
                Assert.IsTrue(new TownBox("paint + epsilon", TownLayout.Paint.XMin - 0.001f, TownLayout.Paint.YMin - 0.001f,
                    TownLayout.Paint.XMax + 0.001f, TownLayout.Paint.YMax + 0.001f).Encloses(view), "camera exposes unpainted space");
            }
            Assert.IsTrue(bounds.Contains(TownLayout.ClampCamera(new TownVec(0, 100), aspect)));
        }

        [Test]
        public void CameraRetainsGameplayScale()
        {
            Assert.AreEqual(8f, TownLayout.CameraSize);
        }

        [Test]
        public void LampsAndLights()
        {
            Assert.AreEqual(12, TownLayout.LampCount);
            for (int i = 0; i < TownLayout.LampCount; i++)
                Assert.IsTrue(TownLayout.Winch.Contains(TownLayout.LampPos(i)), "lamp " + i + " is not on winch");

            Assert.AreEqual(12, TownLayout.LitLamps(0));
            Assert.AreEqual(11, TownLayout.LitLamps(1));
            Assert.AreEqual(0, TownLayout.LitLamps(20));
            Assert.AreEqual(0.75f, TownLayout.WinchLightIntensity(12), 0.00001f);
            Assert.AreEqual(0.30f, TownLayout.WinchLightIntensity(0), 0.00001f);
            var lights = TownLayout.Lights(11);
            Assert.AreEqual(5, lights.Length);
            foreach (var light in lights) Assert.IsTrue(TownLayout.Paint.Contains(light.Pos), light.Name);
            Assert.AreEqual(0.32f, TownLayout.GlobalLight);
            Assert.That(TownLayout.GlobalLight, Is.InRange(TownLayout.GlobalLightMin, TownLayout.GlobalLightMax));
            Assert.AreEqual(3f, TownLayout.PlayerLightRadius);
            Assert.AreEqual(0.2f, TownLayout.PlayerLightIntensity);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace EastTrain
{
    public sealed partial class EastTrainDemo
    {
        EastTrainWorldArtSet worldArt;
        readonly Dictionary<string, Sprite> paintedSprites = new Dictionary<string, Sprite>();
        readonly List<(SpriteRenderer source, SpriteRenderer art)> paintedEffects = new List<(SpriteRenderer, SpriteRenderer)>();
        Transform horizonArt;
        SpriteRenderer paintedTraveller;
        Sprite travellerIdle, travellerWalk, travellerWork;
        Transform fuelNeedle, temperatureNeedle;

        Sprite PaintedSprite(string key)
        {
            if (paintedSprites.TryGetValue(key, out var found)) return found;
            var part = worldArt.Find(key);
            if (part.Texture == null || part.Pixels.width <= 0) return null;
            var sprite = Sprite.Create(part.Texture, part.Pixels, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            paintedSprites[key] = sprite; return sprite;
        }
        Transform Art(Transform parent, string name, string key, float x, float y, float width, float height, int order, bool fit = false)
        {
            var sprite = PaintedSprite(key); if (sprite == null) return null;
            var t = Root(name, parent); t.localPosition = new Vector3(x, y, 0);
            var r = t.gameObject.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sortingOrder = order;
            if (fit) t.localScale = Vector3.one * Mathf.Min(width / sprite.bounds.size.x, height / sprite.bounds.size.y);
            else t.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            return t;
        }
        void HideRenderers(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        void PaintedEffect(Transform source, string key)
        {
            var original = source.GetComponent<SpriteRenderer>();
            var visual = Art(source, "Painted " + source.name, key, 0, 0, 1, 1, original.sortingOrder);
            if (visual == null) return;
            original.enabled = false;
            paintedEffects.Add((original, visual.GetComponent<SpriteRenderer>()));
        }
        void PaintPickup(Transform root, int kind)
        {
            HideRenderers(root);
            Art(root, "Painted fuel", kind == 1 ? "coal" : "logs", 0, 0, .85f, .55f, 26, true);
        }

        void BuildWorldArtwork()
        {
            worldArt = Resources.Load<EastTrainWorldArtSet>("EastTrainWorldArtSet");
            if (worldArt == null) return;
            HideRenderers(far); HideRenderers(middle); HideRenderers(ground);
            var field = world.Find("Snowfield"); if (field != null) HideRenderers(field);
            horizonArt = Root("Painted distant panorama", world);
            for (int i = -1; i <= 1; i++) Art(horizonArt, "Snow horizon", "snow-horizon", i * 84, 4, 84.05f, 28, -95);
            for (int i = -3; i <= 18; i++)
            {
                Art(world, "Painted snowfield", "snow-ground", i * 24, -10.5f, 24.05f, 16, -70);
                Art(ground, "Painted track", "rail-strip", i * 24, -2.93f, 24.05f, .56f, 2);
            }
            foreach (var pickup in pickups) if (pickup.Root != null) PaintPickup(pickup.Root, pickup.Kind);
            PaintStation(world.Find("Abandoned station"), 12, 6);
            PaintStation(world.Find("Eastern depot"), 18, 9);
            for (int i = 0; i < 13; i++)
            {
                Art(ground, "Painted route marker", "marker", 22 + i * 25, -1.3f, 1.3f, 2.7f, -20);
                Art(ground, "Painted wayside rocks", "rocks", 11 + i * 27, -3.6f, 2.3f, 1.05f, 3, true);
            }
            HideRenderers(snowBank);
            Art(snowBank, "Painted snow obstruction", "snow-drift", 0, -1.6f, 5, 2.5f, 8);

            // Replace the blockout surfaces while preserving all interaction transforms.
            var keep = new HashSet<Renderer>();
            foreach (var node in new[] { flame, steam, engineFire, radioLamp, brakeLamp, furnaceGlow, headlight,
                                        engineRotor, cylinderRod, coolingValve, steamCross, needle.parent, lever,
                                        fuelColumn, heatColumn, driveBeacon })
                foreach (var r in node.GetComponentsInChildren<Renderer>(true)) keep.Add(r);
            foreach (var bolt in bolts) keep.Add(bolt.GetComponent<Renderer>());
            foreach (Transform child in train)
                if (child.name.StartsWith("Painted ")) foreach (var r in child.GetComponentsInChildren<Renderer>()) keep.Add(r);
            foreach (var r in train.GetComponentsInChildren<SpriteRenderer>(true))
                if (!keep.Contains(r)) r.enabled = false;

            Art(train, "Painted rear hull", "train-bay", -4.66f, -.15f, 9.6f, 4, 10);
            Art(train, "Painted front hull", "train-bay", 4.66f, -.15f, 9.6f, 4, 10);
            Art(train, "Painted driving cabin", "train-cab", 5.5f, 2.15f, 6.8f, 3, 12);
            Art(train, "Painted chimney", "chimney", -4.4f, 2.5f, 1.1f, 2, 13);
            Art(train, "Painted ladder", "ladder", 3.2f, -.35f, .82f, 2.85f, 25);
            Art(train, "Painted driving console", "console", 6.6f, 1.18f, 2.1f, .85f, 20);
            Art(train, "Painted receiver", "radio", 4.45f, 1.25f, .86f, .68f, 21);
            Art(train, "Painted headlamp", "lamp", 9.16f, .3f, .58f, .7f, 25, true);
            Art(train, "Painted snowplow", "plow", 9.55f, -2.16f, 1.5f, 1.22f, 25);
            foreach (var wheel in wheels) Art(wheel, "Painted wheel", "wheel", 0, 0, 1.25f, 1.25f, 18);
            HideRenderers(engineRotor);
            Art(engineRotor, "Painted engine flywheel", "wheel", 0, 0, 1.45f, 1.45f, 22);
            coolingValve.GetComponent<SpriteRenderer>().enabled = false;
            HideRenderers(steamCross);
            Art(steamCross, "Painted valve wheel", "wheel", 0, 0, .52f, .52f, 25);
            for (int i = 0; i < stock.Count; i++)
            {
                var pile = stock[i];
                pile.localPosition = new Vector3(pile.localPosition.x, -1.45f + (i / 3) * .43f, 0);
                HideRenderers(pile);
                Art(pile, "Painted stored coal", "coal", 0, 0, 1.2f, 1, 22);
            }
            HideRenderers(carried);
            carried.localPosition = new Vector3(.45f, .92f, 0);
            Art(carried, "Painted carried fuel", "logs", 0, 0, 1.4f, 1, 49);
            PaintedEffect(flame, "fire");
            PaintedEffect(engineFire, "fire");
            PaintedEffect(steam, "smoke");
            PaintedEffect(breath, "smoke");
            PaintedEffect(furnaceGlow, "glow");
            PaintedEffect(headlight, "glow");
            foreach (var puff in smoke) PaintedEffect(puff, "smoke");
            PaintedEffect(cylinderRod, "metal-strip");
            PaintedEffect(lever.GetChild(0), "metal-strip");
            PaintedEffect(radioLamp, "glow");
            PaintedEffect(brakeLamp, "glow");
            PaintedEffect(driveBeacon, "glow");
            fuelColumn.GetComponent<SpriteRenderer>().enabled = false;
            heatColumn.GetComponent<SpriteRenderer>().enabled = false;
            fuelNeedle = PaintedGauge("Fuel gauge", -6.02f, .35f, .4f);
            temperatureNeedle = PaintedGauge("Temperature gauge", -.35f, -.65f, .46f);
            needle.parent.Find("Timing case").GetComponent<SpriteRenderer>().enabled = false;
            needle.parent.Find("Timing face").GetComponent<SpriteRenderer>().enabled = false;
            Art(needle.parent, "Painted repair gauge", "gauge", 0, 0, .95f, .95f, 21);

            foreach (Transform child in person)
                if (child != carried && child != breath) HideRenderers(child);
            travellerIdle = PaintedSprite("traveller-idle");
            travellerWalk = PaintedSprite("traveller-walk");
            travellerWork = PaintedSprite("traveller-work");
            var traveller = Art(person, "Painted traveller", "traveller-idle", 0, .75f, 1, 1.5f, 47, true);
            if (traveller != null) paintedTraveller = traveller.GetComponent<SpriteRenderer>();
        }
        Transform PaintedGauge(string name, float x, float y, float size)
        {
            var face = Art(train, name, "gauge", x, y, size, size, 30);
            if (face == null) return null;
            var pivot = Root(name + " needle", train); pivot.localPosition = new Vector3(x, y, 0);
            Box("Gauge hand", pivot, 0, size * .15f, .013f, size * .35f, "#293136", 31);
            Circle("Gauge pin", pivot, 0, 0, .036f, "#8B6746", 32);
            return pivot;
        }
        void PaintStation(Transform station, float width, float height)
        {
            if (station == null) return;
            HideRenderers(station);
            Art(station, "Painted depot", "station", 0, -2.65f + height * .5f, width, height, -32);
        }
        void UpdatePaintedTraveller(bool walking, float step)
        {
            if (paintedTraveller == null) return;
            var sprite = (Driving || Repairing || climbing || Carry > 0) && travellerWork != null ? travellerWork
                : walking && travellerWalk != null && step > 0 ? travellerWalk : travellerIdle;
            paintedTraveller.sprite = sprite;
            paintedTraveller.transform.localScale = Vector3.one * (1.5f / sprite.bounds.size.y);
            paintedTraveller.transform.localPosition = new Vector3(0, .75f + (walking ? Mathf.Abs(step) * .035f : 0), 0);
            paintedTraveller.transform.localRotation = Quaternion.Euler(0, 0, Driving ? -5 : 0);
            if (horizonArt != null)
                horizonArt.position = new Vector3(cam.transform.position.x - Mathf.Repeat(State.Distance * .06f, 84), 0, 0);
            if (fuelNeedle != null) fuelNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(125, -125, State.Fuel / 100));
            if (temperatureNeedle != null) temperatureNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(125, -125, State.Heat / 110));
            foreach (var pair in paintedEffects)
            {
                var tint = pair.source.color;
                pair.art.color = tint;
            }
        }
    }
}

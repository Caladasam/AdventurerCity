using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdventurerCity.View
{
    public sealed class DungeonView : MonoBehaviour
    {
        const float LungeSeconds = 0.25f;
        const float FlashSeconds = 0.18f;
        const float FloatSeconds = 1.2f;
        const float DepthScale = 0.8f;

        static readonly Vector3 origin = new Vector3(-600f, -600f, 0f);

        sealed class Actor
        {
            public Combatant Unit;
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Vector3 Home;
            public Vector2 Size;
            public Color BaseColor = Color.white;
            public Color Tint;
            public Vector3 LungeDirection;
            public float LungeTime = -1f;
            public float FlashTime = -1f;
            public Color FlashColor;
            public float CastTotal = 1f;
            public float DeadTime = -1f;
        }

        struct FloatingText
        {
            public string Text;
            public Vector3 Position;
            public Color Color;
            public float Age;
            public int Size;
        }

        readonly Dictionary<Combatant, Actor> actors = new Dictionary<Combatant, Actor>();
        readonly List<Combatant> stale = new List<Combatant>();
        readonly List<FloatingText> texts = new List<FloatingText>();
        readonly List<Light2D> torches = new List<Light2D>();

        GameRunner runner;
        CameraRig cityRig;
        Camera arenaCamera;
        Transform arenaRoot;
        SpriteRenderer floor;
        SpriteRenderer wall;
        ParticleSystem bursts;
        Light2D flash;
        int floorDungeonId;
        Battle shownBattle;
        int eventCursor;
        float shakeTime;
        float hitStop;
        float flashTime;
        GUIStyle label;
        GUIStyle bigLabel;
        GUIStyle panelStyle;

        public int WatchedExpeditionId { get; private set; }
        public bool IsWatching => WatchedExpeditionId != 0;

        public void Init(GameRunner owner, CameraRig rig)
        {
            runner = owner;
            cityRig = rig;
            arenaRoot = new GameObject("DungeonArena").transform;
            arenaRoot.position = origin;

            floor = PixelArt.Create("Floor", arenaRoot, PixelArt.Stone(new Color32(150, 146, 142, 255)), -32000);
            floor.drawMode = SpriteDrawMode.Tiled;
            floor.size = new Vector2(28f, 12f);
            floor.transform.localPosition = new Vector3(-0.5f, -0.5f, 0f);

            wall = PixelArt.Create("Wall", arenaRoot, PixelArt.Stone(new Color32(110, 104, 100, 255)), -31990);
            wall.drawMode = SpriteDrawMode.Tiled;
            wall.size = new Vector2(28f, 4f);
            wall.transform.localPosition = new Vector3(-0.5f, 7.2f, 0f);

            for (var i = 0; i < 4; i++)
            {
                var x = -9f + i * 6f;
                var pillar = PixelArt.Create("Pillar", arenaRoot, PixelArt.Square(), -31980);
                pillar.color = new Color(0.32f, 0.29f, 0.27f);
                pillar.transform.localScale = new Vector3(1f, 4.2f, 1f);
                pillar.transform.localPosition = new Vector3(x, 6.3f, 0f);
                var torch = Effects.PointLight("Torch", arenaRoot, new Color(1f, 0.62f, 0.3f), 6.5f, 1.2f);
                torch.transform.localPosition = new Vector3(x + 1.6f, 5f, 0f);
                torches.Add(torch);
                var fire = Effects.Smoke(arenaRoot, -31970);
                fire.transform.localPosition = new Vector3(x + 1.6f, 5.1f, 0f);
                var main = fire.main;
                main.startColor = new Color(1f, 0.6f, 0.25f, 0.9f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                var emission = fire.emission;
                emission.rateOverTime = 14f;
            }

            flash = Effects.PointLight("Flash", arenaRoot, new Color(1f, 0.4f, 0.25f), 14f, 0f);
            bursts = Effects.Burst(arenaRoot, 30000, 3f, 0.18f);

            var cameraObject = new GameObject("DungeonCamera");
            arenaCamera = cameraObject.AddComponent<Camera>();
            arenaCamera.orthographic = true;
            arenaCamera.orthographicSize = 6.4f;
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            arenaCamera.nearClipPlane = 0.1f;
            arenaCamera.farClipPlane = 100f;
            arenaCamera.enabled = false;
        }

        public void Watch(int expeditionId)
        {
            WatchedExpeditionId = expeditionId;
            Clear();
            arenaCamera.enabled = true;
            cityRig.Camera.enabled = false;
        }

        public void StopWatching()
        {
            WatchedExpeditionId = 0;
            Clear();
            arenaCamera.enabled = false;
            cityRig.Camera.enabled = true;
        }

        ActiveExpedition Watched()
        {
            foreach (var active in runner.Sim.Expeditions)
                if (active.Record.Id == WatchedExpeditionId)
                    return active;
            return null;
        }

        void Clear()
        {
            foreach (var actor in actors.Values) Destroy(actor.Root.gameObject);
            actors.Clear();
            texts.Clear();
            shownBattle = null;
            eventCursor = 0;
        }

        void Update()
        {
            if (!IsWatching) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopWatching();
                return;
            }

            var active = Watched();
            if (active == null)
            {
                runner.Log.Add(new SimEvent(runner.Sim.State.Tick, "Отряд вернулся из похода"));
                StopWatching();
                return;
            }

            var run = active.Run;
            if (floorDungeonId != run.Dungeon.Id)
            {
                floorDungeonId = run.Dungeon.Id;
                var tint = FloorColor(run.Dungeon.Id) * 3.2f;
                tint.a = 1f;
                floor.color = tint;
                wall.color = tint * 0.8f;
            }
            if (run.Battle != shownBattle)
            {
                foreach (var actor in actors.Values)
                    if (!actor.Unit.IsHero)
                        Destroy(actor.Root.gameObject);
                stale.Clear();
                foreach (var unit in actors.Keys)
                    if (!unit.IsHero)
                        stale.Add(unit);
                foreach (var unit in stale) actors.Remove(unit);
                shownBattle = run.Battle;
                eventCursor = 0;
            }

            SyncActors(run);
            if (shownBattle != null) ConsumeEvents(shownBattle);
            Animate();
            AnimateLights();
            PlaceCamera();
        }

        static Vector3 ArenaPoint(float x, float depth) => new Vector3(x, depth * DepthScale, 0f);

        void SyncActors(Expedition run)
        {
            int tanks = 0, backline = 0, melee = 0, backCount = 0;
            foreach (var hero in run.Party)
                if (IsBackline(hero.Role))
                    backCount++;
            foreach (var hero in run.Party)
            {
                Vector3 home;
                if (hero.Role == CombatRole.Tank)
                {
                    home = ArenaPoint(-1.4f, (tanks % 2 == 0 ? 1f : -1f) * (tanks / 2 + (tanks % 2)) * 1.6f);
                    tanks++;
                }
                else if (IsBackline(hero.Role))
                {
                    home = ArenaPoint(-6f, (backline++ - (backCount - 1) * 0.5f) * 2f);
                }
                else
                {
                    home = ArenaPoint(-2.8f, (melee % 2 == 0 ? 1f : -1f) * (2f + melee / 2 * 1.6f));
                    melee++;
                }
                if (!actors.ContainsKey(hero)) Spawn(hero, home);
            }

            if (shownBattle == null) return;
            var enemies = shownBattle.Enemies;
            var bossRoom = run.RoomIndex < run.Rooms.Count && run.Rooms[run.RoomIndex].Kind == RoomKind.Boss;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (actors.ContainsKey(enemies[i])) continue;
                var home = bossRoom && i == 0 ? ArenaPoint(4.2f, 0f) : ArenaPoint(2.4f + i % 2 * 1.6f, (i - (enemies.Count - 1) * 0.5f) * 2.3f);
                if (bossRoom && i >= 1) home = ArenaPoint(2.2f, i % 2 == 0 ? 3f : -3f);
                Spawn(enemies[i], home);
            }
        }

        void Spawn(Combatant unit, Vector3 home)
        {
            var size3 = unit.IsHero ? new Vector3(0.9f, 1.6f, 0.9f) : EnemySize(unit.Enemy);
            var root = new GameObject(unit.Name).transform;
            root.SetParent(arenaRoot, false);
            var body = PixelArt.Create("Body", root, PixelArt.Character(unit.IsHero ? unit.Class.ToString() : unit.Enemy.ToString()));
            var spriteSize = body.sprite.bounds.size;
            var scale = size3.y / spriteSize.y;
            body.transform.localScale = new Vector3(scale * (unit.IsHero ? 1f : -1f), scale, 1f);
            var size = new Vector2(spriteSize.x * scale, size3.y);
            var shadow = PixelArt.Create("Shadow", root, PixelArt.Dot());
            shadow.color = new Color(0f, 0f, 0f, 0.45f);
            shadow.transform.localScale = new Vector3(size.x * 1.3f, size.x * 0.45f, 1f);
            actors[unit] = new Actor
            {
                Unit = unit,
                Root = root,
                Body = body,
                Shadow = shadow,
                Home = home,
                Size = size,
                Tint = unit.IsHero ? HeroColor(unit.Class) : EnemyColor(unit.Enemy)
            };
        }

        void Emit(Actor actor, Color color, int count)
        {
            if (actor == null) return;
            var parameters = new ParticleSystem.EmitParams
            {
                position = actor.Root.position + Vector3.up * (actor.Size.y * 0.5f),
                startColor = color,
                applyShapeToPosition = true
            };
            bursts.Emit(parameters, count);
        }

        void ConsumeEvents(Battle battle)
        {
            var events = battle.Events;
            for (; eventCursor < events.Count; eventCursor++)
            {
                var e = events[eventCursor];
                if (e.Kind == BattleEventKind.Enrage)
                {
                    shakeTime = 0.3f;
                    Flash(new Color(1f, 0.25f, 0.2f), 2.5f);
                    foreach (var actor in actors.Values)
                        if (!actor.Unit.IsHero && actor.Unit.Active)
                            AddText(actor, "ЯРОСТЬ!", new Color(1f, 0.3f, 0.2f), 18);
                    continue;
                }
                actors.TryGetValue(e.Target ?? e.Source, out var target);
                Actor source = null;
                if (e.Source != null) actors.TryGetValue(e.Source, out source);
                switch (e.Kind)
                {
                    case BattleEventKind.Hit:
                        if (source != null && target != null && source != target && e.Source.Cast == CastKind.None)
                        {
                            source.LungeDirection = (target.Home - source.Home).normalized;
                            source.LungeTime = 0f;
                        }
                        Flash(target, new Color(1f, 0.35f, 0.3f));
                        Emit(target, e.Target.IsHero ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.92f, 0.7f), e.Crit ? 14 : 5);
                        if (e.Crit) hitStop = Mathf.Max(hitStop, 0.07f);
                        AddText(target, e.Crit ? $"{Mathf.CeilToInt(e.Amount)}!" : $"-{Mathf.CeilToInt(e.Amount)}",
                            e.Crit ? new Color(1f, 0.85f, 0.2f) : e.Target.IsHero ? new Color(1f, 0.45f, 0.4f) : Color.white, e.Crit ? 20 : 15);
                        break;
                    case BattleEventKind.Dodge:
                        AddText(target, "уклон", new Color(0.7f, 0.85f, 1f), 13);
                        break;
                    case BattleEventKind.Heal:
                        Flash(target, new Color(0.5f, 1f, 0.5f));
                        Emit(target, new Color(0.45f, 1f, 0.5f), 6);
                        AddText(target, $"+{Mathf.CeilToInt(e.Amount)}", new Color(0.45f, 1f, 0.45f), 15);
                        break;
                    case BattleEventKind.CastStart:
                        if (source != null) source.CastTotal = Mathf.Max(0.1f, e.Amount);
                        break;
                    case BattleEventKind.Interrupt:
                        Emit(target, new Color(0.6f, 0.8f, 1f), 10);
                        AddText(target, "Прервано!", new Color(0.6f, 0.8f, 1f), 17);
                        break;
                    case BattleEventKind.Ability:
                        if (Classes.Get(e.Ability).Cast <= 0f && e.Ability != Ability.Revive)
                        {
                            Emit(source, Color.Lerp(source != null ? source.Tint : Color.white, Color.white, 0.4f), 6);
                            AddText(source, Classes.Get(e.Ability).Name, new Color(0.75f, 0.85f, 1f), 13);
                        }
                        break;
                    case BattleEventKind.Revived:
                        Emit(target, new Color(1f, 0.95f, 0.6f), 18);
                        AddText(target, "Поднят!", new Color(1f, 0.95f, 0.6f), 17);
                        break;
                    case BattleEventKind.Downed:
                        AddText(target, "Упал!", new Color(1f, 0.4f, 0.3f), 18);
                        break;
                    case BattleEventKind.Killed:
                        Emit(target, target != null ? target.Tint : Color.white, 20);
                        hitStop = Mathf.Max(hitStop, 0.05f);
                        break;
                    case BattleEventKind.Died:
                        AddText(source, "Погиб", new Color(1f, 0.2f, 0.2f), 20);
                        break;
                    case BattleEventKind.Summon:
                        Emit(source, new Color(1f, 0.7f, 0.3f), 16);
                        AddText(source, "Подмога!", new Color(1f, 0.7f, 0.3f), 18);
                        break;
                    case BattleEventKind.EnemyAoe:
                        shakeTime = 0.45f;
                        hitStop = Mathf.Max(hitStop, 0.1f);
                        Flash(new Color(1f, 0.55f, 0.25f), 3f);
                        AddText(source, CombatData.Get(e.Source.Enemy).AoeName.ToUpper() + "!", new Color(1f, 0.6f, 0.2f), 24);
                        foreach (var actor in actors.Values)
                            if (actor.Unit.IsHero)
                            {
                                Flash(actor, new Color(1f, 0.6f, 0.3f));
                                Emit(actor, new Color(1f, 0.55f, 0.25f), 8);
                            }
                        break;
                }
            }
        }

        void Flash(Color color, float intensity)
        {
            flash.color = color;
            flash.intensity = intensity;
            flashTime = 0.35f;
        }

        void Flash(Actor actor, Color color)
        {
            if (actor == null) return;
            actor.FlashTime = 0f;
            actor.FlashColor = color;
        }

        void AddText(Actor actor, string text, Color color, int size)
        {
            if (actor == null) return;
            var jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.2f), 0f);
            texts.Add(new FloatingText
            {
                Text = text,
                Position = actor.Root.position + Vector3.up * (actor.Size.y + 0.4f) + jitter,
                Color = color,
                Size = size
            });
        }

        void Animate()
        {
            var dt = Time.deltaTime;
            if (hitStop > 0f)
            {
                hitStop -= dt;
                dt = 0f;
            }
            foreach (var actor in actors.Values)
            {
                var unit = actor.Unit;
                var offset = Vector3.zero;
                if (actor.LungeTime >= 0f)
                {
                    actor.LungeTime += dt;
                    if (actor.LungeTime >= LungeSeconds) actor.LungeTime = -1f;
                    else offset = actor.LungeDirection * (Mathf.Sin(Mathf.PI * actor.LungeTime / LungeSeconds) * 0.9f);
                }
                else if (unit.Active)
                {
                    offset.y = Mathf.Abs(Mathf.Sin(Time.time * 3f + actor.Home.x)) * 0.05f;
                }

                var color = actor.BaseColor;
                if (unit.IsCasting) color = Color.Lerp(color, new Color(1f, 0.85f, 0.45f), 0.25f + 0.25f * Mathf.Sin(Time.time * 12f));
                if (actor.FlashTime >= 0f)
                {
                    actor.FlashTime += dt;
                    if (actor.FlashTime >= FlashSeconds) actor.FlashTime = -1f;
                    else color = Color.Lerp(actor.FlashColor, color, actor.FlashTime / FlashSeconds);
                }

                var rotation = Quaternion.identity;
                if (unit.Downed)
                {
                    rotation = Quaternion.Euler(0f, 0f, unit.IsHero ? 90f : -90f);
                    color *= 0.6f;
                }
                if (unit.Dead)
                {
                    if (actor.DeadTime < 0f) actor.DeadTime = 0f;
                    actor.DeadTime += dt;
                    rotation = Quaternion.Euler(0f, 0f, unit.IsHero ? 90f : -90f);
                    color = Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f), Mathf.Clamp01(actor.DeadTime * 2f));
                    if (!unit.IsHero) color.a = 1f - Mathf.Clamp01((actor.DeadTime - 1f) * 1.5f);
                }
                color.a = Mathf.Min(1f, color.a);

                actor.Root.localPosition = actor.Home + offset;
                actor.Body.transform.localRotation = rotation;
                actor.Body.transform.localPosition = unit.Downed || unit.Dead ? new Vector3(0f, actor.Size.x * 0.5f, 0f) : Vector3.zero;
                actor.Body.color = color;
                var order = PixelArt.SortOrder(actor.Home.y);
                actor.Body.sortingOrder = order;
                actor.Shadow.sortingOrder = order - 1;
                actor.Shadow.enabled = !(unit.Dead && !unit.IsHero && actor.DeadTime > 1f);
            }

            for (var i = texts.Count - 1; i >= 0; i--)
            {
                var text = texts[i];
                text.Age += Time.deltaTime;
                text.Position += Vector3.up * (Time.deltaTime * 1.1f);
                if (text.Age >= FloatSeconds) texts.RemoveAt(i);
                else texts[i] = text;
            }
        }

        void AnimateLights()
        {
            for (var i = 0; i < torches.Count; i++)
                torches[i].intensity = 1.1f + 0.25f * Mathf.PerlinNoise(Time.time * 4f, i * 7.3f);
            if (flashTime > 0f)
            {
                flashTime -= Time.deltaTime;
                flash.intensity = Mathf.Max(0f, flash.intensity - Time.deltaTime * 8f);
            }
            else
            {
                flash.intensity = 0f;
            }
        }

        void PlaceCamera()
        {
            var position = origin + new Vector3(-0.5f, 0.9f, -10f);
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                var jitter = Random.insideUnitCircle * 0.18f;
                position += new Vector3(jitter.x, jitter.y, 0f);
            }
            arenaCamera.transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        void OnGUI()
        {
            if (!IsWatching) return;
            var active = Watched();
            if (active == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, richText = true };
                bigLabel = new GUIStyle(label) { fontStyle = FontStyle.Bold };
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0.11f, 0.12f, 0.14f, 0.94f));
                background.Apply();
                panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = background } };
            }

            foreach (var actor in actors.Values)
            {
                var unit = actor.Unit;
                if (!unit.IsHero && unit.Dead && actor.DeadTime > 1.5f) continue;
                var head = arenaCamera.WorldToScreenPoint(actor.Root.position + Vector3.up * (actor.Size.y + 0.35f));
                var x = head.x;
                var y = Screen.height - head.y;

                var barColor = !unit.IsHero ? new Color(0.8f, 0.3f, 0.3f)
                    : unit.HpRatio > 0.5f ? new Color(0.35f, 0.8f, 0.35f)
                    : unit.HpRatio > 0.25f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.95f, 0.3f, 0.25f);
                DrawBar(new Rect(x - 32f, y - 8f, 64f, 7f), unit.Dead ? 0f : unit.HpRatio, barColor);
                if (unit.Absorb > 0f) DrawBar(new Rect(x - 32f, y - 1f, 64f, 3f), Mathf.Clamp01(unit.Absorb / 40f), new Color(0.9f, 0.9f, 1f));

                var name = unit.IsHero ? $"{unit.Name.Split(' ')[0]} · {CombatData.Get(unit.Class).Name}" : unit.Name;
                GUI.Label(new Rect(x - 90f, y - 26f, 180f, 18f), name, label);

                if (unit.IsCasting)
                {
                    var progress = 1f - unit.CastTimer / actor.CastTotal;
                    GUI.Label(new Rect(x - 90f, y + 4f, 180f, 18f), $"<color=#FFD180>{CastName(unit)}</color>", label);
                    DrawBar(new Rect(x - 32f, y + 22f, 64f, 5f), progress, new Color(1f, 0.8f, 0.4f));
                }
            }

            foreach (var text in texts)
            {
                var point = arenaCamera.WorldToScreenPoint(text.Position);
                var alpha = 1f - text.Age / FloatSeconds;
                var style = new GUIStyle(bigLabel) { fontSize = text.Size };
                var color = text.Color;
                color.a = alpha;
                style.normal.textColor = color;
                GUI.Label(new Rect(point.x - 80f, Screen.height - point.y - 12f, 160f, 24f), text.Text, style);
            }

            var run = active.Run;
            var header = new Rect(Screen.width * 0.5f - 260f, 40f, 520f, 64f);
            GUI.Box(header, GUIContent.none, panelStyle);
            GUI.Label(new Rect(header.x, header.y + 6f, header.width, 22f), $"<b>{run.Dungeon.Name}{(run.Heroic ? " (героич.)" : "")}</b> · {Hud.PhaseText(run)}", label);
            GUI.Label(new Rect(header.x, header.y + 26f, header.width, 18f),
                $"<color=#BBBBBB>{run.Elapsed:0} с в походе · добыча: {run.LootSummary}</color>", label);
            if (GUI.Button(new Rect(header.x + header.width * 0.5f - 90f, header.y + 44f, 180f, 18f), "Вернуться в город (Esc)"))
                StopWatching();

            if (run.Battle == null)
                GUI.Label(new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 30f),
                    $"<size=20><color=#CCCCCC>{(run.Phase == ExpeditionPhase.Rest ? "Привал" : "В пути")}</color></size>", label);
        }

        static void DrawBar(Rect rect, float ratio, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static string CastName(Combatant unit)
        {
            switch (unit.Cast)
            {
                case CastKind.Ability: return Classes.Get(unit.CastAbility).Name;
                case CastKind.EnemyHeal: return "Лечение";
                case CastKind.EnemyAoe: return CombatData.Get(unit.Enemy).AoeName;
                default: return "";
            }
        }

        static bool IsBackline(CombatRole role) => role == CombatRole.Healer || role == CombatRole.Support || role == CombatRole.Ranged;

        static Vector3 EnemySize(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Ratman: return new Vector3(0.75f, 1.0f, 0.75f);
                case EnemyKind.Digger: return new Vector3(1.0f, 1.3f, 1.0f);
                case EnemyKind.Shaman: return new Vector3(0.8f, 1.5f, 0.8f);
                case EnemyKind.Skeleton: return new Vector3(0.7f, 1.6f, 0.7f);
                case EnemyKind.Ghoul: return new Vector3(1.1f, 1.4f, 1.1f);
                case EnemyKind.BoneMage: return new Vector3(0.75f, 1.8f, 0.75f);
                case EnemyKind.CryptKeeper: return new Vector3(2.2f, 3.6f, 2.2f);
                case EnemyKind.Wolf: return new Vector3(0.6f, 0.7f, 1.2f);
                case EnemyKind.PackLeader: return new Vector3(1.1f, 1.3f, 2.2f);
                case EnemyKind.Bandit: return new Vector3(0.9f, 1.5f, 0.9f);
                case EnemyKind.Archer: return new Vector3(0.75f, 1.5f, 0.75f);
                case EnemyKind.Chieftain: return new Vector3(1.6f, 2.6f, 1.6f);
                default: return new Vector3(2.4f, 3.2f, 2.4f);
            }
        }

        static Color HeroColor(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Fighter: return new Color(0.45f, 0.58f, 0.82f);
                case HeroClass.Cleric: return new Color(0.96f, 0.93f, 0.74f);
                case HeroClass.Barbarian: return new Color(0.85f, 0.28f, 0.22f);
                case HeroClass.Paladin: return new Color(0.95f, 0.78f, 0.35f);
                case HeroClass.Druid: return new Color(0.4f, 0.7f, 0.35f);
                case HeroClass.Bard: return new Color(0.85f, 0.45f, 0.75f);
                case HeroClass.Rogue: return new Color(0.3f, 0.3f, 0.35f);
                case HeroClass.Monk: return new Color(0.95f, 0.6f, 0.25f);
                case HeroClass.Ranger: return new Color(0.25f, 0.5f, 0.3f);
                case HeroClass.Wizard: return new Color(0.3f, 0.45f, 0.95f);
                case HeroClass.Sorcerer: return new Color(0.75f, 0.25f, 0.55f);
                default: return new Color(0.45f, 0.25f, 0.65f);
            }
        }

        static Color EnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Ratman: return new Color(0.52f, 0.4f, 0.3f);
                case EnemyKind.Digger: return new Color(0.36f, 0.58f, 0.26f);
                case EnemyKind.Shaman: return new Color(0.55f, 0.32f, 0.75f);
                case EnemyKind.Skeleton: return new Color(0.85f, 0.83f, 0.76f);
                case EnemyKind.Ghoul: return new Color(0.45f, 0.55f, 0.48f);
                case EnemyKind.BoneMage: return new Color(0.35f, 0.75f, 0.7f);
                case EnemyKind.CryptKeeper: return new Color(0.25f, 0.3f, 0.42f);
                case EnemyKind.Wolf: return new Color(0.5f, 0.5f, 0.52f);
                case EnemyKind.PackLeader: return new Color(0.26f, 0.26f, 0.3f);
                case EnemyKind.Bandit: return new Color(0.6f, 0.42f, 0.25f);
                case EnemyKind.Archer: return new Color(0.4f, 0.5f, 0.28f);
                case EnemyKind.Chieftain: return new Color(0.55f, 0.15f, 0.12f);
                default: return new Color(0.3f, 0.24f, 0.2f);
            }
        }

        static Color FloorColor(int dungeonId)
        {
            switch (dungeonId)
            {
                case Dungeons.CatacombsId: return new Color(0.16f, 0.19f, 0.23f);
                case Dungeons.WolfDenId: return new Color(0.2f, 0.26f, 0.17f);
                case Dungeons.BanditCampId: return new Color(0.3f, 0.25f, 0.18f);
                default: return new Color(0.22f, 0.2f, 0.19f);
            }
        }
    }
}

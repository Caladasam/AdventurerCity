using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class DungeonView : MonoBehaviour
    {
        const float LungeSeconds = 0.25f;
        const float FlashSeconds = 0.18f;
        const float FloatSeconds = 1.2f;

        static readonly Vector3 origin = new Vector3(-600f, 0f, -600f);
        static readonly int colorId = Shader.PropertyToID("_Color");
        static readonly int baseColorId = Shader.PropertyToID("_BaseColor");

        sealed class Actor
        {
            public Combatant Unit;
            public Transform Root;
            public MeshRenderer Body;
            public Vector3 Home;
            public Vector3 Size;
            public Color BaseColor;
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
        MaterialPropertyBlock block;

        GameRunner runner;
        CameraRig cityRig;
        Camera arenaCamera;
        Transform arenaRoot;
        MeshRenderer floorRenderer;
        int floorDungeonId;
        Battle shownBattle;
        int eventCursor;
        float shakeTime;
        GUIStyle label;
        GUIStyle bigLabel;
        GUIStyle panelStyle;

        public int WatchedExpeditionId { get; private set; }
        public bool IsWatching => WatchedExpeditionId != 0;

        public void Init(GameRunner owner, CameraRig rig)
        {
            runner = owner;
            cityRig = rig;
            block = new MaterialPropertyBlock();
            arenaRoot = new GameObject("DungeonArena").transform;
            arenaRoot.position = origin;

            var floor = Materials.CreateCube("Floor", arenaRoot);
            floor.transform.localScale = new Vector3(26f, 0.2f, 16f);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            floorRenderer = floor.GetComponent<MeshRenderer>();
            floorRenderer.sharedMaterial = Materials.Get(new Color(0.22f, 0.2f, 0.19f));
            for (var i = 0; i < 6; i++)
            {
                var pillar = Materials.CreateCube("Pillar", arenaRoot);
                pillar.transform.localScale = new Vector3(1.2f, 4f, 1.2f);
                pillar.transform.localPosition = new Vector3(-10f + i * 4f, 2f, 7.2f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = Materials.Get(new Color(0.3f, 0.27f, 0.25f));
            }

            var cameraObject = new GameObject("DungeonCamera");
            arenaCamera = cameraObject.AddComponent<Camera>();
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            arenaCamera.farClipPlane = 200f;
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
                floorRenderer.sharedMaterial = Materials.Get(FloorColor(run.Dungeon.Id));
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
            PlaceCamera();
        }

        void SyncActors(Expedition run)
        {
            int guards = 0, priests = 0, warriors = 0;
            foreach (var hero in run.Party)
            {
                Vector3 home;
                switch (hero.Class)
                {
                    case HeroClass.Guard: home = new Vector3(-1.6f, 0f, guards++ * 1.8f); break;
                    case HeroClass.Priest: home = new Vector3(-6f, 0f, (priests++ - 0.5f) * 2f); break;
                    default: home = new Vector3(-2.6f, 0f, warriors++ % 2 == 0 ? 2f : -2f); break;
                }
                if (!actors.ContainsKey(hero)) Spawn(hero, home);
            }

            if (shownBattle == null) return;
            var enemies = shownBattle.Enemies;
            var bossRoom = run.RoomIndex < run.Rooms.Count && run.Rooms[run.RoomIndex].Kind == RoomKind.Boss;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (actors.ContainsKey(enemies[i])) continue;
                var home = bossRoom && i == 0 ? new Vector3(4.2f, 0f, 0f) : new Vector3(2.4f + i % 2 * 1.6f, 0f, (i - (enemies.Count - 1) * 0.5f) * 2.3f);
                if (bossRoom && i >= 1) home = new Vector3(2.2f, 0f, i % 2 == 0 ? 3f : -3f);
                Spawn(enemies[i], home);
            }
        }

        void Spawn(Combatant unit, Vector3 home)
        {
            var size = unit.IsHero ? new Vector3(0.9f, 1.6f, 0.9f) : EnemySize(unit.Enemy);
            var body = Materials.CreateCube(unit.Name, arenaRoot);
            body.transform.localScale = size;
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(Color.white);
            actors[unit] = new Actor
            {
                Unit = unit,
                Root = body.transform,
                Body = renderer,
                Home = home,
                Size = size,
                BaseColor = unit.IsHero ? HeroColor(unit.Class) : EnemyColor(unit.Enemy)
            };
        }

        void ConsumeEvents(Battle battle)
        {
            var events = battle.Events;
            for (; eventCursor < events.Count; eventCursor++)
            {
                var e = events[eventCursor];
                actors.TryGetValue(e.Target ?? e.Source, out var target);
                actors.TryGetValue(e.Source, out var source);
                switch (e.Kind)
                {
                    case BattleEventKind.Hit:
                        if (source != null && target != null && source != target && e.Source.Cast == CastKind.None)
                        {
                            source.LungeDirection = (target.Home - source.Home).normalized;
                            source.LungeTime = 0f;
                        }
                        Flash(target, new Color(1f, 0.35f, 0.3f));
                        AddText(target, e.Crit ? $"{Mathf.CeilToInt(e.Amount)}!" : $"-{Mathf.CeilToInt(e.Amount)}",
                            e.Crit ? new Color(1f, 0.85f, 0.2f) : e.Target.IsHero ? new Color(1f, 0.45f, 0.4f) : Color.white, e.Crit ? 20 : 15);
                        break;
                    case BattleEventKind.Dodge:
                        AddText(target, "уклон", new Color(0.7f, 0.85f, 1f), 13);
                        break;
                    case BattleEventKind.Heal:
                        Flash(target, new Color(0.5f, 1f, 0.5f));
                        AddText(target, $"+{Mathf.CeilToInt(e.Amount)}", new Color(0.45f, 1f, 0.45f), 15);
                        break;
                    case BattleEventKind.CastStart:
                        if (source != null) source.CastTotal = Mathf.Max(0.1f, e.Amount);
                        break;
                    case BattleEventKind.Interrupt:
                        AddText(target, "Прервано!", new Color(0.6f, 0.8f, 1f), 17);
                        break;
                    case BattleEventKind.Taunt:
                        AddText(source, "Провокация", new Color(0.7f, 0.8f, 1f), 13);
                        break;
                    case BattleEventKind.Revived:
                        AddText(target, "Поднят!", new Color(1f, 0.95f, 0.6f), 17);
                        break;
                    case BattleEventKind.Downed:
                        AddText(target, "Упал!", new Color(1f, 0.4f, 0.3f), 18);
                        break;
                    case BattleEventKind.Died:
                        AddText(source, "Погиб", new Color(1f, 0.2f, 0.2f), 20);
                        break;
                    case BattleEventKind.Summon:
                        AddText(source, "Подмога!", new Color(1f, 0.7f, 0.3f), 18);
                        break;
                    case BattleEventKind.EnemyAoe:
                        shakeTime = 0.45f;
                        AddText(source, CombatData.Get(e.Source.Enemy).AoeName.ToUpper() + "!", new Color(1f, 0.6f, 0.2f), 24);
                        foreach (var actor in actors.Values)
                            if (actor.Unit.IsHero)
                                Flash(actor, new Color(1f, 0.6f, 0.3f));
                        break;
                }
            }
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
            var jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.2f, 0.2f));
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

                var color = actor.BaseColor;
                if (unit.IsCasting) color = Color.Lerp(color, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 12f));
                if (actor.FlashTime >= 0f)
                {
                    actor.FlashTime += dt;
                    if (actor.FlashTime >= FlashSeconds) actor.FlashTime = -1f;
                    else color = Color.Lerp(actor.FlashColor, color, actor.FlashTime / FlashSeconds);
                }

                var rotation = Quaternion.identity;
                var height = actor.Size.y;
                if (unit.Downed)
                {
                    rotation = Quaternion.Euler(0f, 0f, 90f);
                    height = actor.Size.x;
                    color *= 0.6f;
                }
                if (unit.Dead)
                {
                    if (actor.DeadTime < 0f) actor.DeadTime = 0f;
                    actor.DeadTime += dt;
                    rotation = Quaternion.Euler(0f, 0f, 90f);
                    height = actor.Size.x;
                    color = Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f), Mathf.Clamp01(actor.DeadTime * 2f));
                    if (!unit.IsHero && actor.DeadTime > 1.5f) offset.y -= Mathf.Min(1f, actor.DeadTime - 1.5f) * height;
                }

                actor.Root.localPosition = actor.Home + offset + Vector3.up * (height * 0.5f);
                actor.Root.localRotation = rotation;
                block.SetColor(colorId, color);
                block.SetColor(baseColorId, color);
                actor.Body.SetPropertyBlock(block);
            }

            for (var i = texts.Count - 1; i >= 0; i--)
            {
                var text = texts[i];
                text.Age += dt;
                text.Position += Vector3.up * (dt * 1.1f);
                if (text.Age >= FloatSeconds) texts.RemoveAt(i);
                else texts[i] = text;
            }
        }

        void PlaceCamera()
        {
            var position = origin + new Vector3(-0.5f, 7f, -8.2f);
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                position += Random.insideUnitSphere * 0.25f;
            }
            arenaCamera.transform.SetPositionAndRotation(position, Quaternion.Euler(42f, 0f, 0f));
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
                var head = arenaCamera.WorldToScreenPoint(actor.Root.position + Vector3.up * (actor.Size.y * 0.5f + 0.35f));
                if (head.z < 0f) continue;
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
                if (point.z < 0f) continue;
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
                case CastKind.Heal: return "Лечение";
                case CastKind.Revive: return "Поднять";
                case CastKind.EnemyHeal: return "Лечение";
                case CastKind.EnemyAoe: return CombatData.Get(unit.Enemy).AoeName;
                default: return "";
            }
        }

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
                case HeroClass.Guard: return new Color(0.45f, 0.58f, 0.82f);
                case HeroClass.Priest: return new Color(0.96f, 0.93f, 0.74f);
                default: return new Color(0.85f, 0.28f, 0.22f);
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

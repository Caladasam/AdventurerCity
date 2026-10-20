using System.Collections.Generic;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class PixelSprites
    {
        public static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            { 'k', new Color32(28, 22, 26, 255) },
            { 'z', new Color32(20, 18, 22, 255) },
            { 's', new Color32(240, 200, 160, 255) },
            { 'S', new Color32(200, 150, 120, 255) },
            { 'g', new Color32(110, 160, 70, 255) },
            { 'G', new Color32(70, 110, 50, 255) },
            { 'w', new Color32(235, 235, 228, 255) },
            { 'W', new Color32(190, 186, 170, 255) },
            { 'r', new Color32(200, 50, 40, 255) },
            { 'R', new Color32(130, 30, 30, 255) },
            { 'b', new Color32(60, 100, 200, 255) },
            { 'B', new Color32(35, 60, 130, 255) },
            { 'y', new Color32(232, 196, 72, 255) },
            { 'Y', new Color32(170, 130, 40, 255) },
            { 'm', new Color32(170, 175, 185, 255) },
            { 'M', new Color32(100, 105, 115, 255) },
            { 'n', new Color32(130, 85, 50, 255) },
            { 'N', new Color32(85, 55, 35, 255) },
            { 'l', new Color32(160, 110, 70, 255) },
            { 'p', new Color32(150, 70, 170, 255) },
            { 'P', new Color32(80, 40, 110, 255) },
            { 'o', new Color32(230, 130, 50, 255) },
            { 'O', new Color32(170, 85, 30, 255) },
            { 'f', new Color32(60, 130, 60, 255) },
            { 'F', new Color32(35, 80, 40, 255) },
            { 'h', new Color32(90, 60, 40, 255) },
            { 'e', new Color32(255, 60, 40, 255) },
            { 'c', new Color32(90, 230, 220, 255) },
            { 'T', new Color32(50, 120, 125, 255) },
            { 'v', new Color32(225, 130, 255, 255) },
            { 'x', new Color32(130, 130, 135, 255) },
            { 'X', new Color32(75, 75, 82, 255) },
            { 'q', new Color32(120, 255, 120, 255) },
            { 't', new Color32(200, 170, 120, 255) }
        };

        public static readonly Dictionary<string, string[]> Figures = new Dictionary<string, string[]>
        {
            ["Fighter"] = new[]
            {
                "......kkkk......",
                ".....kmmmmk.....",
                ".....kMmmMk.....",
                ".....kszzsk.....",
                ".....kssssk.....",
                "....kkbbbbkk....",
                "..kkkbbbbbbkkm..",
                ".kyykbbbbbbkkm..",
                ".kyYkbbbbbbkkm..",
                ".kyykbbyybbkkm..",
                "..kkkbbbbbbk.m..",
                "....knnnnnnk.k..",
                "....kbbkkbbk....",
                "....kbbk.kbbk...",
                "....kMMk.kMMk...",
                "....kkkk.kkkk..."
            },
            ["Paladin"] = new[]
            {
                "......kkkk......",
                ".....kyyyyk.....",
                ".....kyYYyk.....",
                ".....kszzsk.....",
                ".....kssssk.....",
                "....kkwwwwkk....",
                ".kkkkwwyywwkk.y.",
                "kwwwkwwyywwkkyyy",
                "kwrwkwwyywwkk.y.",
                "krrrkwwwwwwkk.m.",
                "kwrwkwwwwwwk..m.",
                ".kwk.knnnnk...m.",
                "..k..kmmkmmk....",
                ".....kmmkmmk....",
                ".....kMMkMMk....",
                ".....kkkkkkk...."
            },
            ["Cleric"] = new[]
            {
                "......kkkk...yk.",
                ".....kwwwwk.kyyk",
                ".....kwsswk..yk.",
                ".....kszzsk..mk.",
                ".....kssssk..m..",
                "....kkwwwwkk.m..",
                "...kwwwyywwwkm..",
                "...kwwwyywwwsm..",
                "...kwwwwwwwwkm..",
                "...kwwwyywwwkm..",
                "...kwwwwwwwwkm..",
                "...kwwwwwwwwkm..",
                "...kwwyyyywwkm..",
                "...kwwwwwwwwkm..",
                "....kkMMMMkk.m..",
                "....kkkkkkkk.k.."
            },
            ["Druid"] = new[]
            {
                "....k......k.fk.",
                "....kn...nk.kffk",
                ".....knkknk..nk.",
                ".....kffffk..n..",
                ".....kszzsk..n..",
                ".....kssssk..n..",
                "....kkffffkk.n..",
                "...kffFffFffsn..",
                "...kffFffFffkn..",
                "...kfffnnfffkn..",
                "...kffFffFffkn..",
                "...kffffffffkn..",
                "...kffFffFffkn..",
                "...kffffffffkn..",
                "....kkNNNNkk.n..",
                "....kkkkkkkk.k.."
            },
            ["Bard"] = new[]
            {
                "........yy......",
                "......kkky......",
                ".....krrrrk.....",
                "....krrrrrrk....",
                ".....kszzsk.....",
                ".....kssssk.....",
                "....kkppppkk....",
                "...kppppppppk...",
                "..ksppnnnnppk...",
                "..k.kNnnnnNk....",
                "....knnkknnk....",
                "....kNnnnnNk....",
                "....kkppppkk....",
                "....kpppkpppk...",
                "....kNNk.kNNk...",
                "....kkkk.kkkk..."
            },
            ["Barbarian"] = new[]
            {
                "...w........w...",
                "...ww.kkkk.ww...",
                "....wkmmmmkw....",
                ".....kmmmmk.....",
                ".....kszzsk.....",
                ".....khsshk.....",
                "...kkkhhhhkkk.mm",
                "..kssssssssssmMm",
                "..kskssssssksmm.",
                "..ks.kssssk.sk..",
                "..ks.kSSSSk.sm..",
                "....kxxxxxxk.m..",
                "....kxxkkxxk.m..",
                "....kssk.kssk...",
                "....kNNk.kNNk...",
                "....kkkk.kkkk..."
            },
            ["Rogue"] = new[]
            {
                "......kkkk......",
                ".....kzzzzk.....",
                "....kzzzzzzk....",
                "....kzsezszk....",
                "....kzzzzzzk....",
                ".....kXXXXk.....",
                "....kkXXXXkk....",
                "..mkXXXXXXXXkm..",
                "..mkXXnnnnXXkm..",
                "...kXXXXXXXXk...",
                "....kXXXXXXk....",
                "....kNNNNNNk....",
                "....kXXkkXXk....",
                "....kXXk.kXXk...",
                "....kzzk.kzzk...",
                "....kkkk.kkkk..."
            },
            ["Monk"] = new[]
            {
                "......kkkk......",
                ".....kssssk.....",
                ".....kssssk.....",
                ".....kszzsk.....",
                ".....kssssk.....",
                "....kkoooOkk....",
                "..kkooooOoookk..",
                ".kssoooOoooossk.",
                ".kssoooOoooossk.",
                "..kkooOoooookk..",
                "....knnnnnnk....",
                "....kookkook....",
                "....kook.kook...",
                "....kssk.kssk...",
                "....kkkk.kkkk..."
            },
            ["Ranger"] = new[]
            {
                "......kkkk......",
                ".....kffffk.....",
                "....kffffffk....",
                "....kfszzsfk....",
                ".k...kssssk.....",
                ".kn.kkffffkk.nn.",
                ".k.nkffffffknNk.",
                ".k..kffnnffkknk.",
                ".k..kffnnffkk.k.",
                ".k.nkffffffk....",
                ".kn..kffffk.....",
                ".k...knnnnk.....",
                ".....kffkffk....",
                ".....kffkffk....",
                ".....kNNkNNk....",
                ".....kkkkkkk...."
            },
            ["Wizard"] = new[]
            {
                ".......k.....cc.",
                "......kbk...kcck",
                ".....kbbbk...cc.",
                "....kbbybbk..n..",
                "...kkkkkkkkk.n..",
                ".....kszzsk..n..",
                ".....kswwsk..n..",
                "....kkbwwbkk.n..",
                "...kbbbwwbbbsn..",
                "...kbbbbbbbbkn..",
                "...kbbbyybbbkn..",
                "...kbbbbbbbbkn..",
                "...kbbbbbbbbkn..",
                "...kBBBBBBBBkn..",
                "....kkkkkkkk.k.."
            },
            ["Sorcerer"] = new[]
            {
                "......kkkk......",
                ".....khhhhk.....",
                "....khhhhhhk....",
                "....khszzshk....",
                ".....kssssk.....",
                "....kkppppkk.vv.",
                "...kpppppppksvvv",
                "...kppyppyppk.vv",
                "...kpppppppkk...",
                "...kpppyypppk...",
                "...kppppppppk...",
                "....kPPPPPPk....",
                "....kppkkppk....",
                "....kppk.kppk...",
                "....kPPk.kPPk...",
                "....kkkk.kkkk..."
            },
            ["Warlock"] = new[]
            {
                "...k........k...",
                "...kk.kkkk.kk...",
                "....kkPPPPkk....",
                ".....kPPPPk.....",
                ".....kPqqPk.....",
                ".....kPPPPk..qq.",
                "....kkPPPPkk.qqq",
                "...kPPPPPPPPkqq.",
                "...kPPpPPpPPsk..",
                "...kPPPPPPPPk...",
                "...kPPPyyPPPk...",
                "...kPPPPPPPPk...",
                "...kPPPPPPPPk...",
                "...kPPPPPPPPk...",
                "....kkkkkkkk...."
            },
            ["Ratman"] = new[]
            {
                ".kk....kk...",
                "kxxk..kxxk..",
                ".kxxkkxxk...",
                "..kxxxxxk...",
                "..kxexexk...",
                "..kxxxxxkk..",
                "...kxwwk....",
                "..kxxxxxk...",
                ".kxxxxxxxk..",
                ".kxxxxxxxk..",
                "..kxxkxxk..k",
                "..kxxkxxk.kx",
                "..kkkkkkkkx."
            },
            ["Digger"] = new[]
            {
                "....kkkkk.....",
                "...kmmmmmk....",
                "kk.kgggggk.kk.",
                "kgkkgegegkkgk.",
                ".kgggggggggk..",
                "...kgwwwgk..mm",
                "..kkllllkk.mMm",
                ".kgkllllkgkkm.",
                ".kg.kllllk.km.",
                "....kNNNNk.km.",
                "....kgkkgk....",
                "....kgk.kgk...",
                "....kkk.kkk..."
            },
            ["Shaman"] = new[]
            {
                ".kwwk.........",
                "kwzwzk..kkk...",
                ".kwwk..kgggk..",
                "..nk.kkgegegkk",
                "..n.kggggggggk",
                "..n...kgwwgk..",
                "..n.kkppppppk.",
                "..ngkppppppppk",
                "..n.kppyyppppk",
                "..n.kppppppppk",
                "..n.kppppppppk",
                "..n.kPPPPPPPPk",
                "..k..kkkkkkkk."
            },
            ["Grok"] = new[]
            {
                ".......kkkkkk.......",
                "......kyyyyyyk......",
                ".....kmmmmmmmmk.....",
                ".kk..kggggggggk..kk.",
                "kggk.kggeggeggk.kggk",
                ".kgkkggggggggggkkgk.",
                "..kgggggwwwwgggggk..",
                "...kkkgggggggggkkk..",
                "..kmmmmmmmmmmmmmmk..",
                ".kgmmMMMMMMMMMMmmgk.",
                "kggkmmmmmmmmmmmmkggk",
                "kgg.kmmmmyymmmmk.ggk",
                "kgg.kmmmmmmmmmmk.gnk",
                ".kk.kllllllllllk.knk",
                "....kNNNNNNNNNNk.knk",
                "....kggggkkggggkMMMk",
                "....kggggk.kgggkMMMk",
                "....kggggk.kgggkMMMk",
                "....kMMMMk.kMMMk.kk.",
                "....kkkkkk.kkkkk...."
            },
            ["Skeleton"] = new[]
            {
                "....kkkkk.....",
                "...kwwwwwk....",
                "...kwzwzwk....",
                "...kwwwwwk....",
                "....kwzwk.....",
                "...kkkwkkk....",
                "..kw.kwk.wk.m.",
                "..kw.kwkwwk.m.",
                "..k.kwwwk.wkm.",
                ".....kwk...kmk",
                "....kwwwk..kk.",
                "....kw.wk.....",
                "....kw.wk.....",
                "...kkk.kkk...."
            },
            ["Ghoul"] = new[]
            {
                "....kkkk........",
                "...kxxxxk.......",
                "..kxexexk.......",
                "..kxxxxxkkk.....",
                "...kxwkxxxxk....",
                "..kkxxxxxxxxk...",
                ".kWkxxxxxxxxxk..",
                "kW.kxxXXxxxxxk..",
                ".kWkxxxxxxxxk...",
                "...kkxxkkxxk....",
                "....kxxk.kxk....",
                "...kWWk..kWWk...",
                "...kkk...kkk...."
            },
            ["BoneMage"] = new[]
            {
                "....kkkkk...cc.",
                "...kTTTTTk.kcck",
                "...kwwwwwk..cc.",
                "...kwzwzwk..n..",
                "...kwwwwwk..n..",
                "..kkTTTTTkk.n..",
                ".kTTTTTTTTTkwn.",
                ".kTTwTTwTTTk.n.",
                ".kTTTTTTTTTk.n.",
                ".kTTTTTTTTTk.n.",
                ".kTTTwwwTTTk.n.",
                ".kTTTTTTTTTk.n.",
                "..kkkkkkkkk..k."
            },
            ["CryptKeeper"] = new[]
            {
                "...........y.y.y....",
                "...........yyyyy....",
                "..........kMMMMMk...",
                "..........kzezezk...",
                "..........kzzzzzk...",
                ".mmmm....kkMMMMMkk..",
                "mMk.mm..kMMMMMMMMMk.",
                "..k...mkMMMMyMMMMMMk",
                "..k....kMMMMyMMMMMMk",
                "..k...kMMMMMMMMMMMk.",
                "..k...kMMMPPPPPMMMk.",
                "..k..kMMMPPPPPPPMMk.",
                "..k..kMMPPPPPPPPPMk.",
                "..k..kMPPPPPPPPPPPk.",
                "..k..kPPPPPPPPPPPPk.",
                "..k..kPPPPPPPPPPPPPk",
                "..k..kkkkkkkkkkkkkkk"
            },
            ["Wolf"] = new[]
            {
                "..............k.k...",
                ".............kxkxk..",
                "............kxxxxxk.",
                "k...........kxexxxkk",
                "kxk.....kkkkxxxxxxzk",
                ".kxkkkkkxxxxxxxxkkk.",
                "..kxxxxxxxxxxxxxk...",
                "...kxxxxxxxxxxxxk...",
                "...kxwwxxxxxxwwxk...",
                "...kxk.kxk.kxk.kxk..",
                "...kxk.kxk.kxk.kxk..",
                "...kkk.kkk.kkk.kkk.."
            },
            ["Bandit"] = new[]
            {
                "......kkkk......",
                ".....krrrrk.....",
                ".....krRrrkr....",
                ".....kszzsk.r...",
                ".....krrrrk.....",
                "....kknnnnkk....",
                "..kknnnlnnnnkm..",
                "..ksnnnlnnnnkm..",
                "...knnnlnnnnkm..",
                "...knnnnnnnksm..",
                "....kNNyNNNk.m..",
                "....knnkknnk.k..",
                "....knnk.knnk...",
                "....kNNk.kNNk...",
                "....kkkk.kkkk..."
            },
            ["Villager"] = new[]
            {
                "..kkkk..",
                ".khhhhk.",
                ".kszzsk.",
                ".kssssk.",
                "kkbbbbkk",
                "ksbbbbsk",
                "kkbbbbkk",
                ".knnnnk.",
                ".knkknk.",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villagess"] = new[]
            {
                "..kkkk..",
                ".khhhhkk",
                ".kszzshk",
                ".kssssk.",
                "kkrrrrkk",
                "ksrrrrsk",
                "kkrrrrkk",
                ".krrrrrk",
                "krrrrrrk",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Child"] = new[]
            {
                ".kkkk.",
                "khhhhk",
                "kszzsk",
                "kkyykk",
                "ksyysk",
                ".knnk.",
                ".kkkk."
            },
            ["PackLeader"] = new[]
            {
                "..............k.k...",
                ".............kXkXk..",
                "............kXXXXXk.",
                "k...........kXeXXXkk",
                "kXk.....kkkkXXXXXXzk",
                ".kXkkkkkXXXXXXXXkkk.",
                "..kXXXXXXXXXXXXXk...",
                "...kXXXXXXXXXXXXk...",
                "...kXwwXXXXXXwwXk...",
                "...kXk.kXk.kXk.kXk..",
                "...kXk.kXk.kXk.kXk..",
                "...kkk.kkk.kkk.kkk.."
            },
            ["Archer"] = new[]
            {
                "......kkkk......",
                ".....kllllk.....",
                "....kllllllk....",
                "....klszzslk....",
                ".k...kssssk.....",
                ".kn.kkllllkk.nn.",
                ".k.nkllllllknNk.",
                ".k..kllnnllkknk.",
                ".k..kllnnllkk.k.",
                ".k.nkllllllk....",
                ".kn..kllllk.....",
                ".k...knnnnk.....",
                ".....kllkllk....",
                ".....kllkllk....",
                ".....kNNkNNk....",
                ".....kkkkkkk...."
            },
            ["Chieftain"] = new[]
            {
                "......kkkk......",
                ".....kyyyyk.....",
                ".....kyYyyky....",
                ".....kszzsk.y...",
                ".....kyyyyk.....",
                "....kkrrrrkk....",
                "..kkrrrxrrrrkm..",
                "..ksrrrxrrrrkm..",
                "...krrrxrrrrkm..",
                "...krrrrrrrksm..",
                "....kRRyRRRk.m..",
                "....krrkkrrk.k..",
                "....krrk.krrk...",
                "....kRRk.kRRk...",
                "....kkkk.kkkk..."
            },
            ["Villager1"] = new[]
            {
                "..kkkk..",
                ".kyyyyk.",
                ".kszzsk.",
                ".kssssk.",
                "kkffffkk",
                "ksffffsk",
                "kkffffkk",
                ".knnnnk.",
                ".knkknk.",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villager2"] = new[]
            {
                "..kkkk..",
                ".kzzzzk.",
                ".kszzsk.",
                ".kssssk.",
                "kkllllkk",
                "ksllllsk",
                "kkllllkk",
                ".knnnnk.",
                ".knkknk.",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villager3"] = new[]
            {
                "..kkkk..",
                ".knnnnk.",
                ".kszzsk.",
                ".kssssk.",
                "kkttttkk",
                "ksttttsk",
                "kkttttkk",
                ".knnnnk.",
                ".knkknk.",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villagess1"] = new[]
            {
                "..kkkk..",
                ".kyyyykk",
                ".kszzsyk",
                ".kssssk.",
                "kkppppkk",
                "ksppppsk",
                "kkppppkk",
                ".kpppppk",
                "kppppppk",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villagess2"] = new[]
            {
                "..kkkk..",
                ".kzzzzkk",
                ".kszzszk",
                ".kssssk.",
                "kkbbbbkk",
                "ksbbbbsk",
                "kkbbbbkk",
                ".kbbbbbk",
                "kbbbbbbk",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Villagess3"] = new[]
            {
                "..kkkk..",
                ".knnnnkk",
                ".kszzsnk",
                ".kssssk.",
                "kkooookk",
                "ksoooosk",
                "kkooookk",
                ".koooook",
                "kooooook",
                ".kNk.kNk",
                ".kkk.kkk"
            },
            ["Child1"] = new[]
            {
                ".kkkk.",
                "kzzzzk",
                "kszzsk",
                "kkggkk",
                "ksggsk",
                ".knnk.",
                ".kkkk."
            },
            ["Merchant"] = new[]
            {
                "..kkkk..",
                ".kyyyyk.",
                ".kszzsk.",
                ".kssssk.",
                "kkppppkk",
                "ksppppsk",
                "kkppppkk",
                ".kPPPPk.",
                ".kPkkPk.",
                ".kNk.kNk",
                ".kkk.kkk"
            }
        };

        public static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>
        {
            ["mug"] = new[]
            {
                ".wwwww...",
                "kwwwwwk..",
                "kyyyyykk.",
                "kyYyyyk.k",
                "kyyyYyk.k",
                "kyyyyykk.",
                "kkkkkkk.."
            },
            ["scales"] = new[]
            {
                "....k....",
                "kkkkkkkkk",
                "k...k...k",
                "yk..k..ky",
                "yyy.k.yyy",
                "....k....",
                "..kkkkk.."
            },
            ["anvil"] = new[]
            {
                "..........",
                ".kkkkkkkk.",
                "kMMMMMMMMk",
                ".kkMMMMkk.",
                "...kMMk...",
                "..kMMMMk..",
                ".kkkkkkkk."
            },
            ["logs"] = new[]
            {
                ".kkk.kkk...",
                "knNnknNnk..",
                "kNnNkNnNk..",
                ".kkkkkkkk..",
                "..knNnknNnk",
                "..kNnNkNnNk",
                "...kkk.kkk."
            },
            ["saw"] = new[]
            {
                "..kmk..",
                ".kmmmk.",
                "kmmMmmk",
                "mmMkMmm",
                "kmmMmmk",
                ".kmmmk.",
                "..kmk.."
            },
            ["bread"] = new[]
            {
                "kkkkkkkk",
                "knnnnnnk",
                "kn.oo.nk",
                "knooOonk",
                "kn.oo.nk",
                "knnnnnnk",
                "kkkkkkkk"
            },
            ["furnace"] = new[]
            {
                ".kkkkkk.",
                "kMMMMMMk",
                "kMkookMk",
                "kMoyyoMk",
                "kMoooOMk",
                "kkkkkkkk"
            },
            ["banner"] = new[]
            {
                "k......",
                "kyyyyy.",
                "kymymy.",
                "kyymyy.",
                "kymymy.",
                "kyyyyy.",
                "ky...y.",
                "k......"
            },
            ["flag"] = new[]
            {
                "krrrr.",
                "krrrrr",
                "krrrr.",
                "k.....",
                "k....."
            },
            ["crates"] = new[]
            {
                "kkkkk.kkkk",
                "klllk.knnk",
                "klNlk.knnk",
                "klllkkkkkk",
                "kkkkknnnnk",
                "....knNNnk",
                "....kkkkkk"
            },
            ["wheat"] = new[]
            {
                "y.y.y",
                "yYyYy",
                ".YyY.",
                "..Y.."
            },
            ["cross"] = new[]
            {
                "..ky..",
                ".kyyk.",
                "kyyyyk",
                ".kyyk.",
                "..ky..",
                "..ky..",
                "..ky.."
            },
            ["window"] = new[]
            {
                ".kkk.",
                "kyyyk",
                "kyYyk",
                "kyyyk",
                "kkkkk"
            },
            ["scarecrow"] = new[]
            {
                "..yy..",
                ".knnk.",
                "kntntk",
                "..tt..",
                "..n..."
            }
        };
    }
}

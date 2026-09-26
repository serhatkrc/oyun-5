from lib import *

# ---------------- TILES ----------------
T = [
# id, ad, level, walk, water, build, burn, cost, biome, renkler, not
("deep_ocean","Derin Okyanus",0,0,1,0,0,0,0,"#1B3A6B,#1A386A,#1C3C6E,#193667","Sadece gemi; derin su yaratıkları"),
("ocean","Okyanus",1,0,1,0,0,0,0,"#24508A,#234E88,#26538D,#224C85","Gemi rotası"),
("shallow","Sığ Su",2,1,1,0,0,3,1,"#3A78B5,#3875B2,#3D7CB9,#3672AE","Yüzülebilir, yavaş yürünür; mercan biyomu alabilir"),
("sand","Kum",3,1,0,1,0,1.2,1,"#E3D08A,#DFCC86,#E7D48F,#DBC882","Kıyı; kaktüs/palmiye"),
("soil_low","Alçak Toprak",4,1,0,1,0,1,1,"#6B8E3D,#678A39,#6F9241,#638635","Ana yaşam alanı"),
("soil_high","Yüksek Toprak",5,1,0,1,0,1,1,"#5E7F35,#5A7B31,#628339,#56772D","Ana yaşam alanı"),
("hills","Tepe",6,1,0,0,0,2,1,"#7D7560,#79715C,#817964,#756D58","Maden damarı çıkar"),
("mountain","Dağ",7,1,0,0,0,4,0,"#6A6A6A,#666666,#6E6E6E,#626262","Yavaş; zengin maden"),
("summit","Zirve",8,0,0,0,0,0,0,"#E8E8E8,#E4E4E4,#ECECEC,#E0E0E0","Geçilmez; kar tutar"),
("lava_hot","Kızgın Lav",-1,0,0,0,0,0,0,"#FF5A1F,#FF4A12,#FF6A2A,#F04010","Değeni yakar; 300 tick sonra lava_mid"),
("lava_mid","Akan Lav",-1,0,0,0,0,0,0,"#E0401A,#D83A16,#E8481E,#D03414","Yakar; 400 tick sonra lava_cool"),
("lava_cool","Soğuyan Lav",-1,1,0,0,0,3,0,"#5A2A20,#56281E,#5E2C22,#52261C","Yürünür, ısıtır; 600 tick sonra dağ/tepe"),
("ice","Buz",-1,1,0,0,0,1.5,0,"#BFE3F2,#BBDFEE,#C3E7F6,#B7DBEA","Donmuş su; sıcakta sığ suya döner"),
("field","Tarla",-1,1,0,0,1,1,0,"#8A6B3A,#866736,#8E6F3E,#826332","Tarım; ekili hâlde buğday feature'ı taşır"),
("scorched","Yanık Toprak",-1,1,0,1,0,1,0,"#3B332B,#373027,#3F372F,#332C23","300–900 tick sonra alçak toprağa döner"),
("pit","Çukur",-1,0,0,0,0,0,0,"#1E1A16,#1C1814,#201C18,#1A1612","Patlama kalıntısı; yağmurla dolup su olur"),
("goo","Yutan Balçık",-1,0,0,0,0,0,0,"#7A7A86,#767682,#7E7E8A,#72727E","Komşu her şeyi yiyerek yayılır (kanunla sınırlı)"),
("wasteland","Çorak Toprak",-1,1,0,1,0,1.2,0,"#8C8466,#888062,#90886A,#847C5E","Radyasyon izi; bitki çıkmaz, zamanla iyileşir"),
]
# Raise/lower targets: leveled tiles step to the neighbouring level; special tiles map explicitly ("" = no change).
LEVELED=[t[0] for t in sorted((t for t in T if t[2]>=0), key=lambda t:t[2])]
SPECIAL_STEPS={"lava_hot":("",""),"lava_mid":("",""),"lava_cool":("mountain",""),"ice":("sand","shallow"),
    "field":("soil_high","sand"),"scorched":("soil_high","sand"),"pit":("soil_low",""),"goo":("",""),"wasteland":("soil_high","sand")}
RELIEF={"deep_ocean":.02,"ocean":.03,"shallow":.04,"hills":.08,"mountain":.08,"summit":.05}
def steps(i,l):
    if l<0: r,lo=SPECIAL_STEPS[i]
    else:
        k=LEVELED.index(i)
        r=LEVELED[k+1] if k+1<len(LEVELED) else ""
        lo=LEVELED[k-1] if k>0 else ""
    return ("tile."+r if r else ""),("tile."+lo if lo else "")
# Nature rules per tile (Bölüm 2.5, 2.8-2.10). Chances are per sample; ticks are simulation ticks.
NATURE={
 "shallow":dict(freezeTo="tile.ice"),
 "ice":dict(meltTo="tile.shallow"),
 "soil_low":dict(onBurnBecomes="tile.scorched"),"soil_high":dict(onBurnBecomes="tile.scorched"),
 "field":dict(onBurnBecomes="tile.scorched"),
 "scorched":dict(recoverTo="tile.soil_low",recoverChance=1/600),
 "wasteland":dict(recoverTo="tile.soil_low",recoverChance=1/5000),
 "pit":dict(recoverTo="tile.shallow",recoverChance=1/50,recoverNeedsWater=True),
 "lava_hot":dict(decayTo="tile.lava_mid",decayTicks=300,flows=True,quenchTo="tile.lava_cool",ignites=True,zoneHeat=10),
 "lava_mid":dict(decayTo="tile.lava_cool",decayTicks=400,flows=True,quenchTo="tile.lava_cool",ignites=True,zoneHeat=10),
 "lava_cool":dict(decayTo="tile.hills",decayToIfHigh="tile.mountain",decayTicks=600,decayFeature="feat.ore_obsidian",decayFeatureChance=.02,zoneHeat=10),
 "goo":dict(spreads=True),
}
tiles=[]
for t in T:
    i,n,l,w,wa,b,bu,c,cb,cols,note=t
    up,down=steps(i,l)
    tiles.append(dict(id="tile."+i,name=n,level=l,walkable=bool(w),water=bool(wa),buildable=bool(b),
        burnable=bool(bu),moveCost=c,canHaveBiome=bool(cb),colors=cols.split(","),note=note,
        reliefShade=RELIEF.get(i,.06),onRaiseBecomes=up,onLowerBecomes=down,
        renderMaterial="water" if wa else ("lava" if i in ("lava_hot","lava_mid") else ""),
        **NATURE.get(i,{})))
save("tiles",tiles,"Tile Tipleri",["id","name","level","walkable","water","moveCost","note"])

# ---------------- RESOURCES ----------------
R = [
# id, ad, tür, besin, değer, kaynak
("wood","Odun","material",0,1,"Ağaç kesimi"),("stone","Taş","material",0,1,"Tepe/dağ madenciliği"),
("clay","Kil","material",0,1,"Bataklık/nehir kıyısı"),("bone","Kemik","material",0,1,"Avlanma, ölüler"),
("leather","Deri","material",0,2,"Avlanma"),("wool","Yün","material",0,2,"Koyun/keçi kırkma"),
("copper","Bakır","ore",0,3,"Tepe damarları"),("iron","Demir","ore",0,4,"Dağ damarları"),
("silver","Gümüş","ore",0,6,"Dağ damarları (nadir)"),("gold","Altın","currency",0,8,"Dağ damarları, ticaret, vergi"),
("skyiron","Gökdemir","ore",0,14,"Meteor düşüş noktaları, zirve damarları"),("starore","Yıldız Cevheri","ore",0,20,"Kristal ve gök biyomları (çok nadir)"),
("obsidian","Obsidyen","ore",0,10,"Soğumuş lav çevresi"),("crystal","Kristal","magic",0,9,"Kristal biyomu"),
("essence","Öz","magic",0,12,"Büyülü biyomlar, ölü büyücüler"),
("wheat","Buğday","food",2,1,"Tarla"),("bread","Ekmek","food",5,2,"Değirmen + fırın"),
("meat","Et","food",4,2,"Avlanma, hayvancılık"),("fish","Balık","food",3,2,"Balıkçılık"),
("berries","Yaban Meyvesi","food",2,1,"Meyve çalıları"),("fruit","Meyve","food",3,1,"Meyve ağaçları"),
("mushroom","Mantar","food",2,1,"Mantar biyomu, orman"),("honey","Bal","food",4,3,"Arı kovanları, bal korusu"),
("milk","Süt","food",2,1,"İnek/keçi"),("eggs","Yumurta","food",2,1,"Tavuk/kuş"),
("candy","Şekerleme","food",3,3,"Şekerleme ülkesi"),("herbs","Şifalı Ot","medicine",1,3,"Çiçek vadisi, orman"),
("salt","Tuz","trade",0,3,"Çöl, kıyı"),("spice","Baharat","trade",0,5,"Cengel, savan"),
]
res=[dict(id="res."+a,name=b,kind=c,nutrition=d,value=e,source=f) for a,b,c,d,e,f in R]
save("resources",res,"Kaynaklar",["id","name","kind","nutrition","value","source"])

# ---------------- FEATURES (ağaç/bitki/damar) ----------------
F = [
# id, ad, tür, verir, yanar, not
("tree_oak","Meşe","tree","wood:3",1,""),("tree_birch","Huş","tree","wood:2",1,""),
("tree_maple","Akçaağaç","tree","wood:3",1,"Sonbahar renkli"),("tree_pine","Çam","tree","wood:3",1,""),
("tree_snowpine","Karlı Çam","tree","wood:2",1,"Kar tutar"),("tree_palm","Palmiye","tree","wood:2|fruit:1",1,"Kıyı"),
("tree_jungle","Dev Cengel Ağacı","tree","wood:4|fruit:1",1,""),("tree_cypress","Bataklık Servisi","tree","wood:2",1,"Suda büyüyebilir"),
("tree_acacia","Akasya","tree","wood:2",1,"Savan"),("cactus","Kaktüs","plant","fruit:1",0,"Dokunana hasar"),
("tree_dead","Kuru Ağaç","tree","wood:1",1,"Çorak biyomlar"),("tree_giant_mushroom","Dev Mantar","tree","mushroom:3",0,""),
("crystal_spire","Kristal Sütun","tree","crystal:2",0,"Kesilmez, madencilikle alınır"),("tree_fairy","Peri Ağacı","tree","wood:2|essence:1",1,"Işıldar"),
("tree_rot","Çürük Gövde","tree","wood:1",1,"Çevresine çürüme yayar"),("tree_ember","Kor Ağacı","tree","wood:2",0,"Hiç sönmeyen kıvılcımlar"),
("tree_candy","Şeker Ağacı","tree","candy:2",0,""),("tree_citrus","Turunç Ağacı","tree","wood:1|fruit:3",1,""),
("tree_cloud","Bulut Ağacı","tree","wood:1|essence:1",0,"Havada süzülür"),("tree_ash","Kül Ağacı","tree","wood:1",0,""),
("tree_hourglass","Kum Saati Ağacı","tree","wood:1|essence:1",1,"Zaman Kıvrımı biyomu"),("void_pillar","Boşluk Sütunu","tree","essence:2",0,""),
("coral","Mercan","plant","fish:1",0,"Sığ suda; balık çeker"),("tree_bone","Kemik Ağacı","tree","bone:3",0,""),
("tree_honeycomb","Petek Ağacı","tree","honey:2|wood:1",1,"Arı üretir"),("tree_volcanic","Bazalt Dikeni","tree","stone:2",0,""),
("grass_tuft","Ot Öbeği","plant","",1,"Otçullar yer"),("flower","Çiçek","plant","herbs:1",1,"Arıları çeker"),
("berry_bush","Meyve Çalısı","plant","berries:2",1,""),("reed","Saz","plant","",1,"Bataklık/kıyı"),
("wheat_crop","Buğday Başağı","crop","wheat:3",1,"Sadece tarla tile'ında"),("herb_patch","Şifalı Ot Öbeği","plant","herbs:2",1,""),
("small_mushroom","Küçük Mantar","plant","mushroom:1",0,""),("garlic_plant","Sarımsak","plant","herbs:1",1,"Kan emicileri iter"),
("ore_copper","Bakır Damarı","ore","copper:6",0,""),("ore_iron","Demir Damarı","ore","iron:6",0,""),
("ore_silver","Gümüş Damarı","ore","silver:4",0,""),("ore_gold","Altın Damarı","ore","gold:4",0,""),
("ore_skyiron","Gökdemir Damarı","ore","skyiron:3",0,"Meteor sonrası da oluşur"),("ore_starore","Yıldız Cevheri Damarı","ore","starore:2",0,""),
("ore_obsidian","Obsidyen Kütlesi","ore","obsidian:4",0,"Lav soğuyunca"),("rock","Kaya","ore","stone:4",0,""),
("salt_flat","Tuz Yatağı","ore","salt:4",0,"Çöl"),("bones_pile","Kemik Yığını","ore","bone:3",0,"Savaş alanları"),
]
# WorldGen ore vein weights on hills/mountains (Bölüm 1.10.3 adım 9); 0 = never placed by worldgen.
VEIN={"ore_copper":.30,"ore_iron":.26,"ore_silver":.09,"ore_gold":.07,"rock":.28}
AQUATIC={"coral","reed"}  # survive (and grow) on water tiles
feat=[]
for a,b,c,d,e,f in F:
    y={}
    for p in d.split("|"):
        if p: k,v=p.split(":"); y["res."+k]=int(v)
    feat.append(dict(id="feat."+a,name=b,kind=c,yields=y,burnable=bool(e),note=f,veinWeight=VEIN.get(a,0),
        aquatic=a in AQUATIC,burnsInto="feat.tree_dead" if c=="tree" and e and a!="tree_dead" else ""))
save("features",feat,"Ağaçlar, Bitkiler ve Maden Damarları",["id","name","kind","yields","note"])

# ---------------- BIOMES ----------------
B = [
# id, ad, özel?, büyüme, sıcaklık(0-1), nem(0-1), ağaçlar, bitkiler, hayvanlar, uygarlık(yaşam bulutu), efekt, birim trait havuzu, kültür havuzu, dil havuzu, din havuzu, zemin renkleri, açıklama
("grassland","Çayır",0,5,.5,.4,"tree_oak","grass_tuft,flower,berry_bush","sheep,rabbit,cow,horse_wild","human","", "cheerful,fertile","cul.farmers,cul.open_hearth","lang.plain_speech","","#6FA13F,#6B9D3B,#73A543,#679937","Dengeli, verimli ovalar."),
("birch","Huşluk",0,5,.35,.5,"tree_birch","grass_tuft,small_mushroom","deer,fox,rabbit","human,elf","","curious,night_owl","cul.forest_keepers","lang.soft_vowels","","#7FAE52,#7BAA4E,#83B256,#77A64A","Beyaz gövdeli serin ormanlar."),
("maple","Akçaağaç Koruluğu",0,5,.55,.6,"tree_maple","grass_tuft,berry_bush","deer,boar,fox","human,elf","", "wise,homebody","cul.harvest_feast","lang.poetic","","#B8752E,#B4712A,#BC7932,#B06D26","Kızıl yapraklı, bereketli korular."),
("forest","Karışık Orman",0,5,.5,.65,"tree_oak,tree_pine","grass_tuft,berry_bush,herb_patch","wolf,bear,deer,boar","elf","", "hunter,agile","cul.forest_keepers,cul.hunters","lang.soft_vowels","rel.green_mother","#3F7A34,#3B7630,#437E38,#37722C","Sık ormanlar; av bol."),
("jungle","Cengel",0,6,.85,.9,"tree_jungle,tree_palm","reed,flower,herb_patch","monkey,snake,frog,parrot","","", "venom,climber","cul.hunters,cul.spice_road","lang.drumming","rel.serpent_coil","#2E7D2E,#2A792A,#328132,#267526","Sıcak, nemli, tehlikeli."),
("swamp","Bataklık",0,6,.6,.95,"tree_cypress,tree_dead","reed,small_mushroom","frog,crocodile,snake,mosquito_swarm","","","sickly,poisonproof","cul.swamp_dwellers","lang.guttural","rel.drowned_god","#4F5E33,#4B5A2F,#536237,#47562B","Hastalık yayılımı +%50."),
("savanna","Savan",0,6,.8,.3,"tree_acacia","grass_tuft","buffalo,hyena,lion,rhino","","","runner,hunter","cul.nomads","lang.drumming","rel.sun_eye","#B5A445,#B1A041,#B9A849,#AD9C3D","Sıcak otlaklar."),
("desert","Çöl",0,6,.95,.05,"cactus,tree_palm","salt_flat","camel,scorpion,lizard","","Su tile'ı buharlaşma şansı; yiyecek -%50","heatproof,iron_stomach","cul.nomads,cul.spice_road","lang.sand_whisper","rel.sun_eye","#E6CB7A,#E2C776,#EACF7E,#DEC372","Kum denizleri, vahalar."),
("rocklands","Kayalık",0,5,.45,.2,"tree_dead","rock","goat,eagle,lizard","dwarf","Maden damarı çıkma şansı x2","miner,stubborn","cul.stone_carvers","lang.hard_consonants","rel.deep_hammer","#8E8878,#8A8474,#928C7C,#868070","Taşlı, madenli yaylalar."),
("tundra","Tundra",0,5,.1,.4,"","grass_tuft","penguin,seal,reindeer,polar_bear","","Soğuk hasarı (korunaksızlara)","coldproof,stubborn","cul.elders_voice","lang.hard_consonants","rel.frost_mother","#C9D6CF,#C5D2CB,#CDDAD3,#C1CEC7","Donmuş düzlükler."),
("snowpine","Karlı Çamlık",0,5,.15,.7,"tree_snowpine","small_mushroom","wolf,polar_bear,reindeer,owl","dwarf","Kar örtüsü kalıcı","coldproof,loner","cul.hunters","lang.hard_consonants","rel.frost_mother","#DDE8E8,#D9E4E4,#E1ECEC,#D5E0E0","Karla kaplı ormanlar."),
("flower","Çiçek Vadisi",0,5,.6,.55,"tree_oak","flower,herb_patch","butterfly,bee,rabbit","elf","Mutluluk +2 (içinde yaşayanlara)","attractive,cheerful","cul.artisans","lang.poetic","rel.green_mother","#8FBF5A,#8BBB56,#93C35E,#87B752","Rengârenk, huzurlu."),
("clover","Yonca Tarlası",0,5,.5,.5,"","grass_tuft,flower","rabbit,sheep,cow","human","Şans +%5","lucky","cul.harvest_feast","lang.plain_speech","","#5DB84A,#59B446,#61BC4E,#55B042","Uğurlu otlaklar."),
("mushroom","Mantar Diyarı",0,6,.5,.8,"tree_giant_mushroom","small_mushroom","shroomling,snail,beetle","","Spor hastalığı riski","night_owl,poisonproof","cul.swamp_dwellers","lang.guttural","rel.spore_choir","#8D5E8A,#895A86,#91628E,#855682","Dev mantarlar ve sporlar."),
("crystal","Kristal Vadisi",0,6,.3,.4,"crystal_spire","","crystal_beetle,snail","","Büyü birimlerine mana +%20","mage_blood,fragile","cul.artisans,cul.scholars","lang.chime","rel.prism","#7FD1E0,#7BCDDC,#83D5E4,#77C9D8","Işık kıran kristal ormanları."),
("enchanted","Peri Ormanı",0,6,.55,.7,"tree_fairy","flower,herb_patch","fairy,deer,butterfly","elf","Yaralar 2x hızlı iyileşir","mage_blood,wise","cul.scholars","lang.poetic","rel.moon_veil","#5FC98E,#5BC58A,#63CD92,#57C186","Işıldayan büyülü orman."),
("corrupted","Kara Çürüme",0,6,.45,.5,"tree_rot","small_mushroom","rot_crawler,rat,crow","","Mutluluk -3; delilik şansı","cursed,cruel","cul.iron_fist","lang.guttural","rel.hollow_king","#4A3B55,#463751,#4E3F59,#42334D","Bozulmuş, lanetli topraklar."),
("infernal","Kor Diyarı",0,6,1,.1,"tree_ember","","ember_imp,fire_elemental,salamander","","Rastgele yangın çıkarır; tile yanmaz","heatproof,bloodthirsty","cul.war_drums","lang.hissing","rel.eternal_flame","#8A2E1E,#862A1A,#8E3222,#822616","Kızgın, yanan diyar."),
("candy","Şekerleme Ülkesi",0,6,.6,.6,"tree_candy","","candy_golem,gummy_bear,rabbit","","Yiyecek bolluğu; diş ağrısı (mutluluk dalgalanır)","glutton,cheerful","cul.harvest_feast","lang.chime","","#F2A7C9,#EEA3C5,#F6ABCD,#EA9FC1","Tatlı, tuhaf bir diyar."),
("citrus","Turunç Bahçesi",0,6,.7,.5,"tree_citrus","flower","parrot,bee,rabbit","","Veba iyileşme şansı +%10","healer,cheerful","cul.spice_road","lang.poetic","","#C9D34A,#C5CF46,#CDD74E,#C1CB42","Ekşi kokulu bahçeler."),
("garlic","Sarımsaklık",0,5,.5,.5,"tree_oak","garlic_plant","rabbit,goat","human","Kan emici birimler giremez","immune,stubborn","cul.elders_voice","lang.plain_speech","","#A8B08A,#A4AC86,#ACB48E,#A0A882","Keskin kokulu tarlalar."),
("celestial","Gök Bahçesi",0,5,.5,.5,"tree_cloud","flower","sky_whale_calf,owl,fairy","","Kutsama şansı; yıldırım çekmez","blessed,holy","cul.scholars","lang.chime","rel.star_choir","#DDE6FF,#D9E2FB,#E1EAFF,#D5DEF7","Bulut ağaçlı kutsal bahçe."),
("ash","Kül Çölü",0,6,.6,.1,"tree_ash","","ash_crawler,crow,rat","","Bitki çıkmaz; hastalık direnci -%20","survivor,gloomy","cul.iron_fist","lang.hissing","rel.hollow_king","#6F6A62,#6B665E,#736E66,#67625A","Yıkımın ardından kalan küller."),
("timewarp","Zaman Kıvrımı",1,5,.5,.5,"tree_hourglass","","clock_crab,snail","","Adım atanın 1 yıl yaşlanma şansı %10","wise,forgetful","cul.elders_voice,cul.youth_cult","lang.backwards","rel.endless_wheel","#9B7FB0,#977BAC,#9F83B4,#9377A8","Zamanın eridiği tuhaf topraklar."),
("void","Sessiz Boşluk",1,5,.5,.5,"void_pillar","","void_moth","","Mana yenilenmez; sesler kısılır","loner,skeptic","cul.scholars","lang.silent_signs","rel.null","#2A2A3A,#262636,#2E2E3E,#222232","Hiçliğin sızdığı alan."),
("coral","Mercan Resifi",1,5,.75,1,"coral","","fish,crab,turtle,seal","","Sadece sığ suda; balık x3","swimmer,waterborn","cul.sea_folk","lang.bubbling","rel.drowned_god","#3FB3B0,#3BAFAC,#43B7B4,#37ABA8","Sığ denizlerde renkli resifler."),
("volcanic","Volkanik Ova",1,6,.9,.2,"tree_volcanic","","salamander,fire_elemental,lizard","dwarf","Obsidyen damarı; ara sıra lav baloncuğu","heatproof,smith","cul.stone_carvers","lang.hard_consonants","rel.eternal_flame","#4B3A36,#473632,#4F3E3A,#43322E","Kül ve bazalt ovaları."),
("bone","Kemik Düzlüğü",1,6,.4,.2,"tree_bone","bones_pile","skeleton,bone_crawler,crow","","Gece iskelet doğma şansı","necro,gloomy","cul.ancestor_halls","lang.hissing","rel.hollow_king","#D8D0BC,#D4CCB8,#DCD4C0,#D0C8B4","Eski savaşların kemik tarlası."),
("honey","Bal Korusu",1,5,.65,.6,"tree_honeycomb","flower","bee,bear,butterfly","","Yiyecek +%30; arı sokması","glutton,cheerful","cul.harvest_feast","lang.buzzing","rel.hive_mind","#E3B23C,#DFAE38,#E7B640,#DBAA34","Petek ağaçlı altın korular."),
]
biomes=[]
for b in B:
    (i,n,sp,g,t,m,tr,pl,an,cv,ef,ut,cu,la,re,col,desc)=b
    biomes.append(dict(id="bio."+i,name=n,special=bool(sp),growStrength=g,temp=t,moisture=m,
      trees=["feat."+x for x in ids(tr)],plants=["feat."+x for x in ids(pl)],
      animals=["sp."+x for x in ids(an)],lifeCloudCivs=["sp."+x for x in ids(cv)],
      effect=ef,unitTraitPool=["tr."+x for x in ids(ut)],culturePool=ids(cu),languagePool=ids(la),
      religionPool=ids(re),groundColors=col.split(","),description=desc))
BIOME_CODE={"infernal":"RandomFire","swamp":"DiseaseBoost","desert":"Evaporate","rocklands":"OreBoost","tundra":"Cold","snowpine":"Cold",
 "flower":"Happy","clover":"Luck","mushroom":"Spores","crystal":"ManaBoost","enchanted":"Healing","corrupted":"Corruption","garlic":"Repel",
 "celestial":"Holy","ash":"NoPlants","timewarp":"Aging","void":"ManaVoid","coral":"FishBoost","volcanic":"LavaBubble","bone":"NightSkeleton","honey":"FoodBoost"}
BIOME_TEMP={"tundra":-8,"snowpine":-8,"infernal":15,"volcanic":8}
for b in biomes:
    k=b["id"][4:]
    b["effectCode"]=BIOME_CODE.get(k,""); b["tempOffset"]=BIOME_TEMP.get(k,0)
    b["snowCover"]=k in ("tundra","snowpine"); b["waterOnly"]=k=="coral"; b["fireproof"]=k=="infernal"
save("biomes",biomes,"Biyomlar",["id","name","growStrength","effect","description"])

# ---------------- CLOUDS ----------------
C=[("rain","Yağmur Bulutu","Yangın söndürür, bitki büyütür, lavı soğutur"),("snow","Kar Bulutu","Kar örtüsü, suyu dondurur"),
("acid","Asit Bulutu","Tile'ı bir seviye eritir, birimlere hasar"),("lava","Lav Bulutu","Kızgın lav damlatır"),
("life","Yaşam Bulutu","Biyomun hayvanlarını/uygarlıklarını doğurur"),("storm","Fırtına Bulutu","Rastgele yıldırım düşürür"),
("ash","Kül Bulutu","Güneşi keser: sıcaklık -5, bitki büyümesi durur"),("blessing","Kutsal Bulut","Altındakilere 'Kutsanmış' statüsü"),
("plague","Veba Sisi","Altındakilere veba bulaştırma şansı"),("rot","Çürük Bulutu","Altındakilere çürük ısırık bulaştırır"),("candy","Şeker Yağmuru","Şekerleme düşürür, yiyecek verir")]
# effectCode = drop behaviour; natural = can form on its own over water (rain/storm/ash by weight, snow when cold).
CLOUD_COLOR={"rain":"#C8D2DC","snow":"#F4F8FC","acid":"#B6E05A","lava":"#E8603C","life":"#9CE89C","storm":"#6E7686",
 "ash":"#8A8580","blessing":"#FFF2B0","plague":"#9FB07A","rot":"#7A6A8A","candy":"#F4B6D6"}
CLOUD_WEIGHT={"rain":.85,"storm":.10,"ash":.05}
# Drop parameters per behaviour (Bölüm 2.7 table): tile/feature spawned and its chance per drop.
CLOUD_DROP={"acid":dict(chance=.15),"lava":dict(dropTile="tile.lava_hot",chance=.05),"candy":dict(dropFeature="feat.tree_candy",chance=.03),
 "storm":dict(chance=.01),"life":dict(chance=.02)}
save("clouds",[dict(id="cloud."+a,name=b,effect=c,effectCode=a,color=CLOUD_COLOR[a],naturalWeight=CLOUD_WEIGHT.get(a,0),
    coldOnly=a=="snow",dropTile=CLOUD_DROP.get(a,{}).get("dropTile",""),dropFeature=CLOUD_DROP.get(a,{}).get("dropFeature",""),
    dropChance=CLOUD_DROP.get(a,{}).get("chance",1.0)) for a,b,c in C],"Bulutlar",["id","name","effect"])

# ---------------- ERAS ----------------
E=[
# id, ad, min,max yıl, oran, sadakat, fikir, doğurganlık%, sıcaklık, bulut aralığı(ay), biyom büyüme, efektler, renk tonu
("dawn","Şafak Çağı",30,60,10,10,10,120,0,6,2,"Barış olasılığı +%30; yeni şehir kurma hızı +","#FFF3D6"),
("scorch","Kavurucu Güneş",30,50,6,0,-5,90,15,24,-1,"Kuraklık, yangın şansı x2, kar varlıkları yanar","#FFD08A"),
("rain","Yağmur Çağı",30,50,6,5,0,110,-5,2,3,"Küresel yağmur: yangınlar söner, bitki patlaması","#9FB7CF"),
("ice","Buz Devri",30,70,4,0,0,70,-25,8,0,"Su donar, biyom yayılımı durur, açlık artar","#CFE8FF"),
("shadow","Gölge Çağı",30,60,5,-5,-5,90,-5,12,0,"Canavar doğuşu x3, gece iskeletleri","#6E6A8C"),
("blood","Kan Çağı",30,50,5,-15,-20,100,0,12,0,"Savaş/isyan olasılığı x2, plot ilerlemesi +%50","#C45C5C"),
("glimmer","Işıltı Çağı",30,60,5,5,5,100,0,8,2,"Büyü gücü +%50, yeni din kurma şansı x2, trait mutasyon +","#C7A6FF"),
("ash","Kül Çağı",30,50,3,-10,-10,60,-10,16,0,"Mutluluk -5, kıtlık, biyom yayılımı durur","#7D7770"),
("pale","Solgun Çağ",30,50,3,-5,-5,80,0,12,-1,"Hastalık bulaşma x2, veba sisleri","#B8C3A8"),
("harvest","Hasat Çağı",30,60,8,10,5,130,5,6,1,"Tarım verimi x1.5, ticaret +","#F2D38A"),
]
eras=[dict(id="era."+a,name=b,minYears=c,maxYears=d,rate=e,loyaltyBonus=f,opinionBonus=g,fertilityPct=h,
      tempShift=i,cloudIntervalMonths=j,biomeGrowthBonus=k,effects=l,tint=m) for a,b,c,d,e,f,g,h,i,j,k,l,m in E]
# Nature modifiers + default era-clock slot (Bölüm 2.12). effectCodes feed later chapters (society, magic, disease).
ERA_NATURE={
 "dawn":dict(defaultSlot=0,effectCodes=["PeaceBoost"]),
 "harvest":dict(defaultSlot=1,effectCodes=["HarvestBoost"]),
 "rain":dict(defaultSlot=2,globalRain=True,plantGrowthMul=1.5,fireSpreadMul=.3,effectCodes=["GlobalRain"]),
 "blood":dict(defaultSlot=3,effectCodes=["WarBoost"]),
 "shadow":dict(defaultSlot=4,effectCodes=["MonsterSpawn"]),
 "glimmer":dict(defaultSlot=5,effectCodes=["MagicBoost"]),
 "scorch":dict(defaultSlot=6,fireSpreadMul=2.0,effectCodes=["SnowDamage","ZombieDecay"]),
 "ice":dict(defaultSlot=7,stopsBiomeGrowth=True,effectCodes=["FireElementalDamage"]),
 "ash":dict(stopsBiomeGrowth=True,plantGrowthMul=0.0,ashCloudChance=.40,effectCodes=["Famine"]),
 "pale":dict(effectCodes=["DiseaseBoost"]),
}
for e in eras:
    n=ERA_NATURE.get(e["id"][4:],{})
    e.update(defaultSlot=n.get("defaultSlot",-1),fireSpreadMul=n.get("fireSpreadMul",1.0),plantGrowthMul=n.get("plantGrowthMul",1.0),
             stopsBiomeGrowth=n.get("stopsBiomeGrowth",False),globalRain=n.get("globalRain",False),
             ashCloudChance=n.get("ashCloudChance",.05),effectCodes=n.get("effectCodes",[]))
save("eras",eras,"Çağlar",["id","name","minYears","maxYears","rate","effects"])

# ---------------- DISASTERS (otomatik) ----------------
D=[
# id, ad, min dünya yaşı, min nüfus, bekleme(yıl), olasılık/yıl, çağ çarpanı, açıklama
("meteor","Göktaşı Düşüşü",5,0,10,.04,"","Rastgele noktaya göktaşı; çukur + gökdemir damarı"),
("earthquake","Deprem",10,0,15,.03,"","Çatlak hattı: tile seviyeleri kayar, binalar hasar alır"),
("tornado","Hortum",5,0,8,.05,"rain:2","Birimleri ve nesneleri savurur, 60–200 tick sürer"),
("volcano","Yanardağ Uyanışı",20,0,25,.02,"scorch:2","Dağda patlama, lav akışı, kül bulutu"),
("tsunami","Dev Dalga",30,50,30,.015,"","Kıyıya su dalgası: sığ su istilası, bina yıkımı"),
("famine","Kıtlık",15,200,20,.03,"ice:3,ash:3","Bölgede tarım verimi -%80, 3–6 yıl"),
("plague","Salgın",20,300,25,.03,"pale:4","Kalabalık bir şehirde veba başlar"),
("demon_raid","Kor İblisi Akını",40,300,40,.015,"shadow:2,blood:2","Kor Diyarı yoksa bile yarık açılır, iblisler çıkar"),
("dark_mages","Kara Büyücü Ayaklanması",30,200,30,.02,"shadow:2","3–6 kara büyücü belirir"),
("dragon","Ejderha Uyanışı",50,0,60,.01,"","Dağ zirvesinden ejderha doğar"),
("sky_visitors","Gökten Gelenler",60,500,60,.008,"glimmer:2","Gökten gemiler iner, birimleri kaçırır"),
("ice_storm","Buz Fırtınası",10,0,15,.02,"ice:3","Geniş alanda donma"),
("heat_wave","Sıcak Dalgası",10,0,15,.02,"scorch:3","Su buharlaşır, yangın şansı x5"),
("locusts","Çekirge Sürüsü",15,100,20,.02,"harvest:2","Tarlaları yer"),
("flood","Sel",10,0,12,.03,"rain:4","Alçak toprakları su basar"),
("zombie_outbreak","Ölü Salgını",40,300,50,.012,"shadow:3,pale:2","Mezarlık/kemik düzlüğünden ölüler kalkar"),("bandits","Haydut Çetesi",20,200,15,.03,"blood:2","Kanunsuzlardan oluşan bağımsız grup doğar"),
]
# placement: where the decision system aims (Bölüm 2.13); power: the handler that executes it (Bölüm 7, "" = not yet mapped).
DIS_PLACE={"meteor":"randomLand","volcano":"highestMountain","tsunami":"coastCity","plague":"biggestCity","famine":"biggestCity",
 "zombie_outbreak":"biggestCity","bandits":"biggestCity","dragon":"randomSummit","demon_raid":"biomeOrLand:bio.infernal"}
DIS_POWER={"meteor":"pw.meteor","earthquake":"pw.earthquake","tornado":"pw.tornado","volcano":"pw.volcano","tsunami":"pw.tsunami",
 "plague":"pw.plague","dragon":"pw.spawn_dragon","dark_mages":"pw.spawn_dark_mage","sky_visitors":"pw.spawn_sky_visitor",
 "ice_storm":"pw.ice_bomb","heat_wave":"pw.heat","flood":"pw.flood","zombie_outbreak":"pw.spawn_zombie","bandits":"pw.spawn_bandit"}
save("disasters",[dict(id="dis."+a,name=b,minWorldAge=c,minPopulation=d,cooldownYears=e,chancePerYear=f,
     eraMultipliers={("era."+x.split(":")[0]):float(x.split(":")[1]) for x in ids(g)},description=h,
     placement=DIS_PLACE.get(a,"randomLand"),power=DIS_POWER.get(a,"")) for a,b,c,d,e,f,g,h in D],
     "Otomatik Afetler",["id","name","minWorldAge","cooldownYears","chancePerYear","description"])

# ---------------- WORLDGEN TEMPLATES (Bölüm 1.10.2) ----------------
def wgt(i,n,mask,octaves=6,freq=1.0,warp=.08,warpFreq=2.0,edge=.06,land=None,**flat):
    d=dict(id="wgt."+i,name=n,mask=mask,noise=dict(octaves=octaves,frequencyMul=freq,lacunarity=2.0,gain=.5),
        warp=dict(amplitude=warp,frequencyMul=warpFreq),edgeOcean=edge,landRatioDefault=land)
    d.update(flat); return d
templates=[
    wgt("continents","Kıtalar",dict(type="multiBlob",count=[2,4],radius=[.25,.4],falloffPower=2.2)),
    wgt("pangea","Tek Kıta",dict(type="radial",count=[1,1],radius=[.42,.48],falloffPower=2.0)),
    wgt("archipelago","Takımadalar",dict(type="none",count=[0,0],radius=[0,0],falloffPower=1.0),freq=2.6,warp=.05,land=.30),
    wgt("ring","Halka",dict(type="annulus",count=[1,1],radius=[.30,.34],falloffPower=1.0,width=.13)),
    wgt("lakes","Göller",dict(type="inverse",count=[1,1],radius=[.5,.5],falloffPower=1.0),edge=.04,land=.70),
    wgt("flat_green","Düz Yeşillik",dict(type="full",count=[0,0],radius=[0,0],falloffPower=1.0),edge=.03,
        flatTile="tile.soil_low",flatBiome="bio.grassland"),
    wgt("empty_ocean","Boş Okyanus",dict(type="empty",count=[0,0],radius=[0,0],falloffPower=1.0),flatTile="tile.deep_ocean",flatBiome=""),
    wgt("custom_image","Görüntüden",dict(type="image",count=[0,0],radius=[0,0],falloffPower=1.0)),
]
save("worldgen_templates",templates,"Harita Şablonları",["id","name","mask"])

# ---------------- BIOME TABLE (Bölüm 1.10.3 adım 7) ----------------
# Rows: temperature 0..1 (6 steps), columns: moisture 0..1 (6 steps).
G=[["tundra","tundra","tundra","snowpine","snowpine","snowpine"],
   ["rocklands","grassland","birch","birch","snowpine","swamp"],
   ["rocklands","grassland","grassland","forest","forest","swamp"],
   ["savanna","grassland","flower","forest","maple","swamp"],
   ["desert","savanna","savanna","forest","jungle","jungle"],
   ["desert","desert","savanna","jungle","jungle","jungle"]]
PATCHES=["clover","mushroom","crystal","enchanted","candy","citrus","garlic","celestial"]
save("biome_table",[dict(id="bt.default",grid=[["bio."+b for b in r] for r in G],patches=["bio."+b for b in PATCHES],
    patchSize=[150,800],patchesPerChunk=.4)],"Biyom Tablosu",["id"])

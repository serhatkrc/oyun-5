from lib import *
G = {
"diet":[("herbivore","Otçul","diet:plant"),("carnivore","Etçil","diet:meat|dmg:+5%"),("omnivore","Hepçil","diet:any"),
 ("insectivore","Böcekçil","diet:insect"),("piscivore","Balıkçıl","diet:fish|flag:fish_hunt"),("scavenger","Leşçil","diet:corpse|immune:food_poison"),
 ("nectar","Nektarcı","diet:flower"),("lithophage","Taş Yiyen","diet:stone"),("photosynthesis","Fotosentez","hungerRate:-70%|needsSun:1"),
 ("brain_eater","Beyin Yiyen","diet:brain"),("fungivore","Mantarcıl","diet:mushroom"),("bloodfeeder","Kan İçen","diet:blood|lifesteal:+10%"),
 ("cannibal","Yamyam","diet:same_species|happinessOthers:-3"),("fasting","Az Yiyen","hungerRate:-40%")],
"repro":[("live_birth","Canlı Doğum","repro:live"),("egg_laying","Yumurtlama","repro:egg"),("budding","Tomurcuklanma","repro:bud|fertility:+30%"),
 ("fission","İkiye Bölünme","repro:split"),("spore_birth","Sporla Üreme","repro:spore"),("parthenogenesis","Eşeysiz Üreme","flag:no_mate_needed"),
 ("twins","İkiz Eğilimi","twinChance:+30%"),("litter","Kalabalık Batın","litterSize:3-6"),("single_offspring","Tek Yavru","litterSize:1|childSurvival:+30%"),
 ("fast_gestation","Kısa Gebelik","gestation:-50%"),("long_gestation","Uzun Gebelik","gestation:+100%|childStats:+10%"),
 ("seasonal_breeding","Mevsimsel Üreme","breedMonths:spring"),("monogamous","Tek Eşli","flag:lifelong_mate|happiness:+2"),("polygamous","Çok Eşli","fertility:+25%")],
"life":[("fast_maturity","Erken Olgunlaşma","maturity:-50%"),("slow_maturity","Geç Olgunlaşma","maturity:+80%|xp:+15%"),
 ("long_lived_kind","Uzun Ömürlü Soy","lifespan:+60%"),("short_lived_kind","Kısa Ömürlü Soy","lifespan:-50%|fertility:+30%"),
 ("ageless","Yaşlanmayan","flag:no_aging"),("metamorphic","Başkalaşımcı","flag:metamorphosis"),("elder_wisdom","Yaşlı Bilgeliği","elderIntel:+3"),
 ("youth_vigor","Genç Dinçliği","youngSpeed:+20%"),("hibernation","Kış Uykusu","winterSleep:1|hungerRate:-50%"),("rebirth","Küllerden Doğuş","onDeath:rebirth:10%")],
"brain":[("primitive_brain","İlkel Beyin","intel:-2|neuron:instinct"),("advanced_memory","Gelişmiş Hafıza","flag:can_hold_culture"),
 ("speech_center","Konuşma Merkezi","flag:can_speak|flag:can_hold_language"),("abstract_thought","Soyut Düşünce","flag:can_hold_religion"),
 ("tool_use","Alet Kullanımı","flag:can_use_items|flag:can_build"),("planning","Planlama","flag:can_plot"),("pattern_seek","Örüntü Arayıcı","bookWrite:+50%"),
 ("empathy","Empati","happinessOthers:+1|spareEnemy:+20%"),("hive_mind","Kovan Zihni","flag:shared_mind|loyalty:+30"),
 ("instinct_driven","İçgüdüsel","neuron:instinct|intel:-1"),("curious_minds","Meraklı Zihinler","neuron:explore|xp:+10%"),
 ("dream_walkers","Rüya Gezginleri","sleepXp:+50%"),("sapience","Bilinç","flag:sapient"),("mimicry","Taklitçilik","learnFromOthers:+50%")],
"social":[("pack_hunter","Sürü Avcısı","packDmg:+20%|neuron:pack_hunt"),("solitary","Yalnız","flag:no_family|dmg:+10%"),
 ("herd","Sürü Güdüsü","neuron:follow_herd|fleeTogether:1"),("colony","Koloni","flag:colony|buildSpeed:+30%"),
 ("territorial","Bölgeci","neuron:guard_territory|aggro:+30%"),("nomadic","Göçebe","neuron:migrate|flag:no_permanent_city"),
 ("xenophile","Yabancı Sever","tolerance:+50%"),("xenophobe","Yabancı Düşmanı","tolerance:-50%"),
 ("matriarchal","Ana Soylu","leaderRule:female"),("patriarchal","Baba Soylu","leaderRule:male"),("egalitarian","Eşitlikçi","happiness:+1|loyalty:+5"),
 ("hierarchical","Hiyerarşik","warfare:+1|happinessLow:-1"),("communal_care","Ortak Bakım","childSurvival:+30%"),("peace_seeking","Barışçıl Soy","warChance:-40%")],
"climate":[("cold_adapted","Soğuğa Uyumlu","immune:cold"),("heat_adapted","Sıcağa Uyumlu","immune:heat"),("arid_adapted","Kuraklığa Uyumlu","thirst:-60%"),
 ("wet_adapted","Neme Uyumlu","swampCost:-50%"),("altitude","Yüksek Rakım","mountainCost:-50%"),("cave_dweller","Mağara Sakini","flag:lives_in_mountains"),
 ("nocturnal","Gececi","activeAt:night|nightBonus:+20%"),("diurnal","Gündüzcü","activeAt:day"),("sun_sensitive","Güneş Hassası","sunDmg:1"),
 ("storm_hardy","Fırtınaya Dayanıklı","immune:lightning"),("toxic_tolerant","Zehre Toleranslı","immune:poison"),("radiation_tolerant","Radyasyona Dayanıklı","immune:radiation")],
"move":[("swimmer_kind","Yüzücü","flag:swim"),("aquatic","Suda Yaşar","flag:water_only|flag:breathe_water"),("amphibious","Amfibi","flag:swim|flag:breathe_water"),
 ("flying","Uçucu","flag:fly"),("gliding","Süzülen","fallDmg:0|jumpRange:+3"),("burrowing","Kazıcı","flag:burrow"),("climbing","Tırmanıcı","mountainCost:-60%"),
 ("fast_runner","Koşucu","speed:+25%"),("slow_mover","Ağır","speed:-30%|armor:+5"),("jumper","Sıçrayıcı","flag:jump"),("migratory","Göçmen","neuron:seasonal_migrate"),("hover","Havada Süzülen","flag:hover")],
"sense":[("keen_sight","Keskin Görüş","sight:+4"),("keen_smell","Keskin Koku","trackRange:+8"),("echolocation","Yankı Algısı","flag:see_in_dark"),
 ("thermal_vision","Isıl Görüş","flag:see_invisible"),("tremorsense","Titreşim Algısı","ambushResist:+50%"),("poor_sight","Zayıf Görüş","sight:-3"),
 ("sixth_sense","Altıncı His","dodge:+10"),("magic_sense","Büyü Algısı","spellResist:+20%")],
"cover":[("fur","Kürk","coldResist:+30%"),("thick_fur","Kalın Kürk","immune:cold|heatDmg:+30%"),("scales","Pullu","armor:+5"),("shell","Kabuklu","armor:+12|speed:-15%"),
 ("exoskeleton","Dış İskelet","armor:+8"),("feathers","Tüylü","coldResist:+20%"),("bare_skin","Çıplak Deri","speed:+5%|armor:-2"),
 ("slime_coat","Balçık Kaplı","grabResist:+100%"),("bark_skin","Kabuk Deri","armor:+6|fireDmg:+50%"),("stone_skin","Taş Deri","armor:+15|speed:-20%"),
 ("spines","Dikenli","reflect:+15%"),("camouflage","Kamuflaj","stealth:+1"),("toxic_skin","Zehirli Deri","onHitTaken:poison"),
 ("regenerating_tissue","Yenilenen Doku","regen:+1"),("thick_bones","Kalın Kemik","hp:+15%|knockbackResist:+50%"),("hollow_bones","Kof Kemik","speed:+10%|hp:-10%")],
"attack":[("claws","Pençe","dmg:+4"),("fangs","Sivri Diş","dmg:+3|bleed:1"),("horns","Boynuz","chargeDmg:+50%"),("tusks","Fildişi Diş","dmg:+5"),
 ("stinger","İğne","onHit:poison:20"),("venom_glands","Zehir Bezi","onHit:poison:40"),("acid_spit","Asit Tükürüğü","spell:acid_spit"),
 ("fire_glands","Ateş Bezi","spell:fire_breath"),("frost_breath","Buz Nefesi","spell:freeze"),("tail_whip","Kuyruk Kamçısı","aoeHit:1"),
 ("crushing_jaw","Ezici Çene","armorPierce:+30%"),("charge","Hücum","neuron:charge"),("ambusher","Pusucu","firstStrikeDmg:+100%"),("electric_organ","Elektrik Organı","onHit:stun:5")],
"metab":[("fast_metabolism","Hızlı Metabolizma","hungerRate:+40%|speed:+10%"),("slow_metabolism","Yavaş Metabolizma","hungerRate:-40%|speed:-10%"),
 ("fat_reserves","Yağ Deposu","starveTime:+100%"),("cold_blooded","Soğukkanlı","coldSlow:1|hungerRate:-30%"),("warm_blooded","Sıcakkanlı","coldResist:+20%"),
 ("efficient_digestion","Verimli Sindirim","foodValue:+30%"),("weak_stomach","Zayıf Mide","foodPoisonChance:+30%"),("water_storage","Su Deposu","immune:drought"),
 ("high_stamina","Yüksek Dayanım","stamina:+50%"),("low_stamina","Düşük Dayanım","stamina:-40%")],
"immune":[("plague_resist","Veba Direnci","diseaseResist:+50%"),("disease_prone","Hastalığa Yatkın","diseaseResist:-40%"),("infection_immune","Çürüğe Bağışık","immune:rotbite"),
 ("spore_immune","Spora Bağışık","immune:spores"),("madness_resist","Cinnet Direnci","immune:black_madness"),("fast_healing","Hızlı İyileşme","regenOutOfCombat:+100%"),
 ("antivenom","Panzehirli Kan","immune:poison"),("curse_ward","Lanet Kalkanı","immune:cursed")],
"size":[("size_tiny","Minik Boy","size:1"),("size_small","Küçük Boy","size:2"),("size_medium","Orta Boy","size:3"),("size_large","İri Boy","size:4"),
 ("size_huge","Devasa Boy","size:5"),("dimorphism","Eşey Farkı","flag:sex_dimorphism")],
"calling":[("artistic","Sanatçı Ruh","happiness:+1|bookWrite:+20%"),("musical","Müzikal","festivalHappiness:+50%"),("builders","İnşaatçı Soy","buildSpeed:+40%"),
 ("miners_kind","Madenci Soy","mineYield:+40%"),("sailors_kind","Denizci Soy","boatSpeed:+30%|colonizeChance:+30%"),("farmers_kind","Çiftçi Soy","farmYield:+40%"),
 ("warrior_kind","Savaşçı Soy","warfare:+2|dmg:+5%"),("mystic_kind","Mistik Soy","mana:+30|faith:+30%"),("traders_kind","Tüccar Soy","tradeProfit:+40%"),("scholars_kind","Âlim Soy","intel:+2")],
"arcane":[("magic_affinity","Büyü Yatkınlığı","spellPower:+30%"),("anti_magic","Büyü Sağırı","spellResist:+60%|flag:no_spells"),("radiant","Işıyan","lightRadius:3|dmgVsUndead:+30%"),
 ("shadow_born","Gölge Doğumlu","nightBonus:+30%|stealth:+1"),("elemental_fire","Ateş Özü","immune:burning|onHit:burn:10"),("elemental_water","Su Özü","flag:breathe_water|fireResist:+50%"),
 ("elemental_earth","Toprak Özü","armor:+8"),("elemental_air","Hava Özü","speed:+15%|flag:hover"),("undying","Ölmeyen","reviveOnce:1"),
 ("soul_bond","Ruh Bağı","mateDeath:mourning_x2|pairDmg:+15%"),("time_touched","Zaman Dokunmuş","agingRandom:1"),("void_touched","Boşluk Dokunmuş","manaDrainAura:1"),
 ("crystal_body","Kristal Beden","armor:+10|flag:shatter_on_death"),("plant_body","Bitkisel Beden","hungerRate:-50%|fireDmg:+50%"),("gold_digest","Altın Sindiren","diet:gold"),
 ("divine_chosen","Tanrı Seçkini","luck:+15|blessChance:+50%"),("mutable","Değişken Genom","mutation:+100%"),("stable_genome","Sabit Genom","mutation:-80%"),
 ("pure","Saf Kan","allStats:+5%|mutation:-50%"),("hybrid_vigor","Melez Gücü","hybridBonus:+15%")],
"behavior":[("builds_nests","Yuva Kurar","neuron:build_nest"),("food_hoarder","Yiyecek Biriktirir","neuron:hoard_food"),("gift_giver","Hediye Verir","neuron:give_gift|opinion:+5"),
 ("grave_keepers","Ölülerini Gömer","neuron:bury_dead|happiness:+1"),("stargazers","Yıldız Gözlemcisi","neuron:stargaze|intel:+1"),("dancers","Dansçılar","neuron:dance|happiness:+2"),
 ("fire_makers","Ateş Yakar","neuron:make_fire|coldResist:+20%"),("domesticators","Evcilleştirici","neuron:tame_animal"),("story_tellers","Hikâye Anlatıcı","neuron:tell_story|xpShare:+10%"),
 ("raiders","Yağmacı","neuron:raid|lootBonus:+50%"),("shiny_collectors","Parlak Toplayıcı","neuron:collect_shiny"),("sun_greeters","Güneşe Selam","neuron:greet_sun|faith:+10%")],
"mark":[("divine_breeding","İlahi Islah","flag:edited"),("uplifted","Yükseltilmiş","flag:uplifted"),("unmoving","Kıpırtısız","speed:0"),
 ("antimatter_core","Karşı Madde Özü","onDeath:explode:huge"),("monolith_touched","Monolit Dokunuşu","xp:+20%|mutation:+30%"),("first_of_kind","İlk Soy","flag:founder_lineage")],
}
NONRANDOM={"divine_breeding","uplifted","unmoving","antimatter_core","first_of_kind"}
items=[]
for cat,lst in G.items():
    for i,n,e in lst:
        items.append(dict(id="sst."+i,name=n,group=cat,effects=mods(e),canAppearRandomly=i not in NONRANDOM))
save("subspecies_traits",items,"Alt Tür Trait'leri",["id","name","group","effects"])
open(os.devnull,"w").write("Medeni olabilmek için: sst.sapience + sst.tool_use. Kültür: advanced_memory; Dil: speech_center; Din: abstract_thought; Plot: planning")

# ---------------- GENES (45) ----------------
GN=[("vitality","Canlılık","hp:+10%"),("might","Kuvvet","dmg:+8%"),("haste","Çabukluk","speed:+6%"),("guard","Muhafız","armor:+3"),
("focus","Odak","crit:+5"),("reach","Erim","range:+1"),("persistence","Süreklilik","lifespan:+12%"),("brood","Döl","fertility:+15%"),
("mind","Zihin","intel:+1"),("charm","Cazibe","diplo:+1"),("battle","Cenk","warfare:+1"),("order","Düzen","steward:+1"),
("mana","Mana","mana:+15"),("mending","Onarım","regen:+0.5"),("stamina","Soluk","stamina:+20%"),("appetite","Tokluk","hungerRate:-12%"),
("rest","Dinçlik","sleepNeed:-20%"),("hardiness","Sertlik","diseaseResist:+15%"),("warmth","Sıcaklık","heatResist:+25%"),("chill","Serinlik","coldResist:+25%"),
("growth","Büyüme","size:+0.5|hp:+5%"),("dwarfism","Küçülme","size:-0.5|dodge:+5"),("calm","Sükûnet","happiness:+1"),("rage","Hiddet","dmg:+12%|happiness:-1"),
("luck","Talih","luck:+5"),("night","Gece","nightBonus:+10%"),("day","Gün","dayBonus:+10%"),("fin","Yüzgeç","swimSpeed:+30%"),
("grip","Kavrama","mountainCost:-20%"),("keen","Keskinlik","sight:+2"),("bones","Kemik","knockbackResist:+25%"),("hide","Post","armor:+2|coldResist:+10%"),
("blood","Kan","bleedResist:+50%"),("nerve","Sinir","atkspd:+8%"),("heart","Yürek","fleeThreshold:-20%"),("lungs","Ciğer","breathHold:+100%"),
("liver","Karaciğer","poisonResist:+40%"),("mutagen","Mutajen","mutation:+50%"),("stabilizer","Dengeleyici","mutation:-50%"),("empty","Boş Dizi",""),
("junk","Hurda Dizi","hp:-3%"),("dominant","Baskın","inheritWeight:+50%"),("recessive","Çekinik","inheritWeight:-50%"),("ancient","Kadim","xp:+15%|lifespan:+5%"),
("chimera","Kimera","randomTraitOnBirth:5%")]
genes=[dict(id="gene."+a,name=b,effects=mods(c)) for a,b,c in GN]
save("genes",genes,"Genler",["id","name","effects"])
SY=[("titan","Titan Uyumu","growth,vitality,bones","hp:+20%|size:+1"),("hunter_line","Avcı Soyu","haste,keen,focus","crit:+10|speed:+5%"),
("sage_line","Bilge Soyu","mind,ancient,persistence","intel:+2|lifespan:+10%"),("berserk_line","Cinnet Soyu","rage,nerve,heart","dmg:+15%"),
("sea_line","Deniz Soyu","fin,lungs,chill","flag:breathe_water"),("frost_line","Ayaz Soyu","chill,hide,stamina","immune:cold"),
("ember_line","Kor Soyu","warmth,blood,rage","immune:burning"),("golden_line","Altın Soy","luck,charm,calm","luck:+15|diplo:+2")]
save("gene_synergies",[dict(id="syn."+a,name=b,requires=["gene."+x for x in ids(c)],bonus=mods(d)) for a,b,c,d in SY],
     "Gen Sinerjileri",["id","name","requires","bonus"])
open(OUT+"/gene_rules.json","w",encoding="utf-8",newline="\n").write(json.dumps({
 "chromosomeCount":{"default":2,"max":4},"slotsPerChromosome":{"default":6,"max":10},
 "mutationPerBirth":0.02,"mutationOps":["swap_gene","replace_random","duplicate","delete_to_empty"],
 "synergyRule":"Aynı kromozomda yan yana dizilen gerekli genler sinerji bonusu açar",
 "inheritance":"Her kromozom ebeveynlerden birinden (ağırlıklı) alınır; 'Baskın' genler ağırlığı artırır"},ensure_ascii=False,indent=1))

# ---------------- PHENOTYPES (50) ----------------
COL=[("sand","Kum","#D8C08A","desert,savanna"),("ash","Kül","#8A8580","ash,volcanic"),("night","Gece","#2E2B3A","void,corrupted"),
("snow","Kar","#EEF2F4","tundra,snowpine"),("crimson","Kızıl","#A23A2E","infernal,maple"),("honey","Bal","#D9A441","honey,flower"),
("olive","Zeytin","#6E7A3A","swamp,forest"),("sea","Deniz","#3E7FA8","coral"),("violet","Menekşe","#7C5AA6","enchanted,timewarp,mushroom"),
("gold","Altın","#E0C04A","celestial,citrus")]
PAT=[("plain","Düz"),("spotted","Benekli"),("striped","Çizgili"),("ringed","Halkalı"),("shaded","Gölgeli")]
ph=[]
for c,cn,hx,bio in COL:
    for p,pn in PAT:
        ph.append(dict(id=f"ph.{c}_{p}",name=f"{cn} {pn}",baseColor=hx,pattern=p,biomeBias=["bio."+b for b in ids(bio)]))
save("phenotypes",ph,"Fenotipler",["id","name","baseColor","pattern","biomeBias"])

# ---------------- EVOLUTION / METAMORPHOSIS ----------------
EV=[("monolith_stage1","Monolit — Uyanış",1,"sst.curious_minds,sst.tool_use","Çevredeki hayvanlara 20 yılda bir"),
("monolith_stage2","Monolit — Söz",2,"sst.speech_center,sst.advanced_memory","Aşama 1'i geçmiş alt türler"),
("monolith_stage3","Monolit — Bilinç",3,"sst.sapience,sst.abstract_thought,sst.planning","Tür medeni olur; ilk köy kurulur"),
("isolation_drift","İzolasyon Kayması",0,"","100 yıl izole popülasyon: %30 ihtimalle yeni alt tür + 1–2 trait"),
("biome_adapt","Biyom Uyumu",0,"","Yeni biyomda doğan ilk nesil biyomun alt tür havuzundan trait alabilir"),
("radiation_mutation","Radyasyon Mutasyonu",0,"","Işınlanmış statüsü doğumda mutasyon oranını x5 yapar")]
save("evolution_rules",[dict(id="evo."+a,name=b,stage=c,grants=ids(d),note=e) for a,b,c,d,e in EV],"Evrim Kuralları",["id","name","stage","grants","note"])
MT=[("caterpillar_to_butterfly","sp.caterpillar","sp.butterfly","age:1","Kozada 3 ay"),
("tadpole_frog","sp.frog","sp.frog","age:0.5","Yavru formu suda"),
("corpse_to_zombie","*","sp.zombie","dis_rotbite","Ölüm anında"),("animal_to_zombie_beast","*animal","sp.zombie_beast","dis_rotbite","Hayvanlar"),("big_to_brute","*size>=4","sp.zombie_brute","dis_rotbite","İri birimler"),("fresh_to_runner","*","sp.zombie_runner","dis_rotbite:%25","Taze ölülerin %25'i"),("dragon_to_rot","sp.dragon","sp.zombie_dragon","dis_rotbite","Ejderha"),("runner_decay","sp.zombie_runner","sp.zombie_crawler","age:3","Koşucu çürüyünce"),("growth_to_mound","*","sp.flesh_mound","dis_fleshgrowth","Hastalık sonu"),
("spores_to_shroom","*","sp.shroomling","dis_spores","Hastalık sonu"),("mage_to_necro","sp.dark_mage","sp.necromancer","age:150","Yaşlı kara büyücü"),
("tree_awaken","feat.tree_oak","sp.walking_tree","era.glimmer","Işıltı çağında 500 yaşındaki meşe uyanabilir"),
("slime_merge","sp.slime","sp.slime","merge:4","4 balçık birleşip büyür")]
save("metamorphoses",[dict(id="meta."+a,**{"from":b},to=c,trigger=d,note=e) for a,b,c,d,e in MT],"Dönüşümler",["id","from","to","trigger","note"])

from lib import *
import json
bio=json.load(open(OUT+"/biomes.json",encoding="utf-8"))["items"]; spc=json.load(open(OUT+"/species.json",encoding="utf-8"))["items"]; clouds=json.load(open(OUT+"/clouds.json",encoding="utf-8"))["items"]
P=[]
def add(i,n,tab,typ,params="",lock="",desc=""): P.append(dict(id="pw."+i,name=n,tab=tab,type=typ,params=mods(params),unlockedBy=lock or None,description=desc))
# --- Dünya
for i,n,t,p,d in [("soil","Toprak","brush","op:raise_to|target:soil_low|interval:3","Suyu kademeli karaya çevirir"),("sand","Kum","brush","op:set|target:sand",""),
("hills","Tepe","brush","op:raise_to|target:hills|interval:4",""),("mountain","Dağ","brush","op:raise_to|target:summit|interval:4",""),
("shallow","Sığ Su","brush","op:lower_to|target:shallow|interval:3",""),("water","Su","brush","op:lower_to|target:ocean|interval:3",""),
("deep_water","Derin Su","brush","op:lower_to|target:deep_ocean|interval:3",""),("raise","Yükselt","brush","op:raise|interval:6","Bir kademe"),
("lower","Alçalt","brush","op:lower|interval:6","Bir kademe"),("sponge","Sünger","brush","op:sponge","Suyu kurutur"),
("road","Yol","brush","op:flag|flag:road",""),("field","Tarla","brush","op:set|target:field",""),("eraser","Silgi","brush","op:erase","Bina, bitki, biyom siler"),
("tree","Ağaç Ek","drop","spawn:biome_tree|density:0.3","Biyomun ağacını eker"),("bush","Çalı Ek","drop","spawn:feat.berry_bush|density:0.3",""),
("flowers","Çiçek Ek","drop","spawn:feat.flower|density:0.4",""),("mushrooms","Mantar Ek","drop","spawn:feat.small_mushroom|density:0.3",""),
("wheat","Buğday Ek","drop","spawn:feat.wheat_crop","Sadece tarlada"),("rock","Kaya","drop","spawn:feat.rock",""),
("ore_copper","Bakır Damarı","drop","spawn:feat.ore_copper",""),("ore_iron","Demir Damarı","drop","spawn:feat.ore_iron",""),
("ore_silver","Gümüş Damarı","drop","spawn:feat.ore_silver",""),("ore_gold","Altın Damarı","drop","spawn:feat.ore_gold",""),
("ore_skyiron","Gökdemir Damarı","drop","spawn:feat.ore_skyiron",""),("ore_starore","Yıldız Cevheri","drop","spawn:feat.ore_starore","")]:
    add(i,n,"world",t,p,"ach.first_mine" if i in("ore_skyiron","ore_starore") else "",d)
for b in bio:
    k=b["id"].split(".")[1]; add("seed_"+k,b["name"]+" Tohumu","world","drop",f"spawn:seed|biome:{b['id']}",
        "ach.biome_master" if b["special"] else "",f"Düştüğü yerde {b['name']} biyomu başlatır")
# --- Medeniyet
for s in [x for x in spc if x["category"]=="civ"]:
    add("spawn_"+s["id"][3:],s["name"],"civ","spawn",f"species:{s['id']}|count:1","",s["note"])
for i,n,t,p,d,l in [("friendship","Dostluk","target","target:kingdom_pair|opinion:+100","İki krallığı barıştırır, ittifak şansı",""),
("spite","Kin","target","target:kingdom_pair|opinion:-100","İki krallığı savaşa sürükler",""),("inspire","İlham","target","target:unit|status:inspired","",""),
("rebellion_spark","İsyan Kıvılcımı","target","target:city|loyalty:-100","Şehir isyan eder",""),("crown","Taç","target","target:unit|makeKing:1","Birimi krallığının kralı yapar","ach.kingmaker"),
("relocate","Sancak Değiştir","target","target:unit|changeKingdom:1","Birimi başka krallığa geçirir",""),("magnet","Kutsal Mıknatıs","target","grab:units|max:50","Birimleri tut-taşı-bırak",""),
("zone_add","Sınır Ekle","brush","op:zone_add","Şehir sınırına zone ekler",""),("zone_remove","Sınır Kaldır","brush","op:zone_remove","",""),
("bless_city","Şehri Kutsa","target","target:city|status:blessed","",""),("curse_city","Şehri Lanetle","target","target:city|status:cursed","",""),
("capital","Başkent Yap","target","target:city|makeCapital:1","",""),("found_village","Köy Kur","spawn","spawn:village","Seçili türden hazır küçük köy",""),
("migrants","Göçmen Dalgası","target","target:city|spawnMigrants:10","",""),("festival","Şenlik Başlat","target","target:city|happiness:+10","","")]:
    add(i,n,"civ",t,p,l,d)
# --- Yaratıklar
for s in [x for x in spc if x["category"]!="civ" and x["id"] not in("sp.caterpillar",)]:
    lock={"sp.dragon":"ach.dragon_age","sp.colossus":"ach.destroyer","sp.sky_visitor":"ach.star_gazer","sp.devourer":"ach.mad_scientist"}.get(s["id"],"")
    add("spawn_"+s["id"][3:],s["name"],"creatures","spawn",f"species:{s['id']}|count:1",lock,s["note"])
# --- Doğa ve afetler
for c in clouds:
    add(c["id"].replace("cloud.","cloud_"),c["name"],"nature","spawn",f"spawn:{c['id']}","ach.plague_lord" if c["id"]=="cloud.plague" else "",c["effect"])
for i,n,t,p,d,l in [("fire","Ateş","drop","fire:120","Tutuşturur",""),("extinguish","Söndür","brush","op:extinguish","",""),
("snow","Kar","drop","flag:snow|freezeWater:1","",""),("ice","Buz","brush","op:freeze","Suyu dondurur",""),("lava","Lav","drop","spawn:tile.lava_hot","",""),
("cool_lava","Lav Soğut","brush","op:cool_lava","",""),("tornado","Hortum","spawn","spawn:tornado|durationTicks:600","",""),
("earthquake","Deprem","target","quake:radius:30","Çatlak hattı oluşturur",""),("tsunami","Dev Dalga","target","wave:dir:auto|power:1","",""),
("volcano","Yanardağ","spawn","spawn:volcano","Yükselir, lav püskürtür",""),("geyser","Gayzer","spawn","spawn:geyser","Sıcak su fışkırtır",""),
("meteor","Göktaşı","drop","explosion:medium|leaves:ore_skyiron","",""),("lightning","Yıldırım","drop","dmg:150|fire:60|stun:10","",""),
("flood","Sel","target","flood:radius:25","",""),("plague","Veba","target","infect:dis_plague","","ach.plague_lord"),
("rotbite","Çürük Isırık","target","infect:dis_rotbite","",""),("fleshgrowth","Et Büyümesi","target","infect:dis_fleshgrowth","",""),
("spores","Spor","target","infect:dis_spores","",""),("madness","Cinnet","target","infect:dis_black_madness","",""),
("zombie_serum","Arınma Serumu","brush","cure:dis_rotbite|removeStatus:turning","Dönüşmekte olanları kurtarır (zombileri değil)",""),("raise_dead","Ölüleri Kaldır","brush","reanimate:corpses|as:zombie","Alandaki cesetler zombi olur","ach.zombie_world"),("divine_light","Kutsal Işık","brush","cure:all|status:blessed","Hastalık, cinnet ve çürüğü temizler",""),
("monolith","Monolit","spawn","spawn:monolith|radius:20|intervalYears:20","Çevredeki hayvanları evrimleştirir","ach.first_civ"),
("golden_brain","Altın Beyin","spawn","spawn:lure|attract:brain_eaters","Zombileri çeker",""),("fertile_rain","Bereket","brush","growPlants:1|fertility:+50%","",""),
("drought","Kuraklık","brush","dryWater:shallow|killPlants:30%","",""),("heat","Isı Dalgası","brush","temp:+20","",""),("cold","Ayaz","brush","temp:-20","",""),
("blessing","Kutsama","target","target:unit|status:blessed","",""),("curse","Lanet","target","target:unit|status:cursed","","")]:
    add(i,n,"nature",t,p,l,d)
# --- Yıkım
for i,n,t,p,d,l in [("bomb","Bomba","drop","explosion:small|radius:4","",""),("dynamite","Dinamit","drop","explosion:small|radius:3|delay:30","Fitil yanar",""),
("napalm","Napalm","drop","fire:255|radius:8","",""),("cluster","Parça Bombası","drop","explosion:small|count:8|spread:10","",""),
("atomic","Atom Bombası","drop","explosion:huge|radius:40|irradiate:1|tile:wasteland","","ach.destroyer"),
("antimatter","Karşı Madde","drop","explosion:void|radius:60|deleteTiles:1","Her şeyi siler","ach.world_eater"),
("heat_ray","Isı Işını","brush","ray:1|dmg:40/tick|fire:1|meltTile:1","",""),("black_hole","Kara Delik","spawn","pull:radius:25|durationTicks:400","","ach.world_eater"),
("fireball","Ateş Topu","drop","explosion:small|fire:150","",""),("goo","Yutan Balçık","drop","spawn:tile.goo","Kanunla sınırlandırılabilir","ach.mad_scientist"),
("ice_bomb","Buz Bombası","drop","freeze:radius:10","",""),("storm","Şimşek Fırtınası","target","lightning:count:20|radius:20","",""),
("acid","Asit Damlası","drop","meltTile:1|dmg:30","",""),("finger","Dürtme Parmağı","target","poke:1|knockback:high","Birimi fırlatır",""),
("stomp","Ezme","drop","dmg:9999|radius:2|crushBuildings:1","",""),("shockwave","Şok Dalgası","drop","knockback:radius:15","","")]:
    add(i,n,"destruction",t,p,l,d)
# --- Diğer
for i,n,t,p,d,l in [("life_game","Hayat Oyunu","brush","automaton:conway","Hücreler tile boyar, birimleri ezer",""),
("ant","Tur Karıncası","spawn","automaton:langton","",""),("fireworks","Havai Fişek","drop","vfx:fireworks|happiness:+2",""," "),
("confetti","Konfeti","drop","vfx:confetti","",""),("inspect","İncele","target","open:inspect","",""),("follow","Takip Et","target","camera:follow","",""),
("possess","Ruh Girişi","target","possess:1","Birimi doğrudan kontrol et","ach.first_civ"),("eye","Keşif Gözü","toggle","highlight:undiscovered_traits","",""),
("stats","Dünya İstatistikleri","window","window:stats","",""),("graphs","Grafikler","window","window:graphs","",""),("history","Dünya Tarihi","window","window:history","",""),
("laws","Dünya Kanunları","window","window:laws","",""),("era_clock","Çağ Saati","window","window:eras","",""),
("ed_unit","Birim Editörü","window","window:editor_unit","",""),("ed_subspecies","Alt Tür Editörü","window","window:editor_subspecies","",""),
("ed_genes","Gen Editörü","window","window:editor_genes","","ach.gene_splicer"),("ed_clan","Klan Editörü","window","window:editor_clan","",""),
("ed_culture","Kültür Editörü","window","window:editor_culture","",""),("ed_religion","Din Editörü","window","window:editor_religion","",""),
("ed_language","Dil Editörü","window","window:editor_language","",""),("ed_kingdom","Krallık Editörü","window","window:editor_kingdom","",""),
("ed_item","Eşya Editörü","window","window:editor_item","",""),("meta_control","Meta Kontrol","target","control:meta","Bir krallığı/dini yönet","ach.puppet_master"),
("save","Kaydet","window","window:save","",""),("load","Yükle","window","window:load","",""),("achievements","Başarımlar","window","window:achievements","",""),
("settings","Ayarlar","window","window:settings","",""),("tutorial","Öğretici","window","window:tutorial","",""),("new_world","Yeni Dünya","window","window:worldgen","","")]:
    add(i,n,"other",t,p,l.strip(),d)
save("powers",P,"Tanrı Güçleri",["id","name","tab","type","unlockedBy","description"])

# ---------------- WORLD LAWS ----------------
WL={"civ":[("kingdom_rebellions","Krallık İsyanları",1),("wars","Savaşlar",1),("diplomacy","Diplomasi",1),("royal_marriages","Kraliyet Evlilikleri",1),
 ("migration","Göç",1),("colonization","Denizaşırı Sömürge",1),("plots","Entrikalar",1),("new_religions","Yeni Dinler",1),("culture_splits","Kültür Ayrılıkları",1),
 ("borders_grow","Sınır Büyümesi",1),("item_crafting","Eşya Üretimi",1),("book_writing","Kitap Yazımı",1),("mixed_species_cities","Karma Tür Şehirleri",1)],
"life":[("animal_spawn","Hayvan Doğal Doğuşu",1),("monster_spawn","Canavar Doğuşu",1),("aging","Yaşlanma",1),("hunger","Açlık",1),("reproduction","Üreme",1),
 ("disease","Hastalık Yayılımı",1),("evolution","Doğal Evrim",1),("metamorphosis","Dönüşümler",1),("population_cap","Nüfus Sınırı",0),("undead_rising","Ölülerin Kalkışı",1),("zombie_apocalypse","Kıyamet Salgını (her ölü zombi olarak kalkar)",0),("zombie_decay","Zombi Çürümesi (zombiler 10 yılda dağılır)",1),("zombie_hordes","Zombi Sürüleri (zombiler toplanıp şehirlere yürür)",1)],
"nature":[("biome_spread","Biyom Yayılımı",1),("tree_growth","Ağaç Büyümesi",1),("fire_spread","Yangın Yayılımı",1),("clouds","Bulutlar",1),
 ("auto_disasters","Doğal Afetler",1),("seasons","Mevsimler",1),("lava_cooling","Lav Soğuması",1),("goo_spread","Balçık Yayılımı",0),("eternal_summer","Sonsuz Yaz",0),("eternal_winter","Sonsuz Kış",0)],
"genetics":[("mutation","Mutasyon",1),("mutant_box","Mutant Kutusu (yeni alt türe 1–4 rastgele trait)",0),("gene_chaos","Gen Kaosu (her doğumda gen karışır)",0),
 ("life_cloud_civs","Yaşam Bulutundan Uygarlık",0),("uplift_all","Herkes Bilinçli",0),("pure_lines","Saf Soylar (mutasyon yok)",0)],
"fun":[("laughing_death","Kahkahalı Ölüm (ölen birim patlar ve konfeti saçar)",0),("tiny_world","Minik Dünya (tüm birimler küçük)",0),("giants","Devler Diyarı",0),
 ("peaceful_world","Barış Dünyası (saldırı yok)",0),("chaos_world","Kaos (herkes herkese düşman)",0),("god_name","Tanrının Adı (metin alanı)",0)],
"forbidden":[("forbidden_codex","Yasak Kodeks — tüm içeriklerin kilidi açılır, bu dünyada başarım kapanır",0)]}
laws=[dict(id="law."+i,name=n,group=g,default=bool(d)) for g,l in WL.items() for i,n,d in l]
for l in laws:
    if l["id"] in("law.auto_disasters","law.population_cap","law.mutation"): l["slider"]={"auto_disasters":[0,3,1],"population_cap":[100,20000,5000],"mutation":[0,5,1]}[l["id"][4:]]
laws[-1]["unlockCondition"]="Tek dünyada aynı anda 333+ 'Saf Kan' alt tür trait'li birim bulundur, sonra Çağ Saati'nde tüm çağları tek tur çevir"
save("world_laws",laws,"Dünya Kanunları",["id","name","group","default"])

# ---------------- ACHIEVEMENTS ----------------
A=[("first_life","İlk Nefes","İlk canlıyı yarat",""),("first_civ","İlk Köy","İlk şehir kurulsun","pw.monolith, pw.possess"),
("first_mine","Derinlerde","İlk maden ocağı","pw.ore_skyiron, pw.ore_starore"),("population_1k","Kalabalık","Dünya nüfusu 1.000",""),
("population_10k","Mahşer","Dünya nüfusu 10.000",""),("kingmaker","Kral Yapıcı","5 farklı krallık kur/taçlandır","pw.crown"),
("empire","İmparatorluk","Tek krallık 25 şehir",""),("ancient_kingdom","Kadim Taht","Bir krallık 500 yıl yaşasın",""),
("world_war","Dünya Savaşı","Aynı anda 5 savaş",""),("peace_age","Altın Barış","100 yıl hiç savaş olmasın",""),
("plague_lord","Vebanın Efendisi","Aynı anda 1.000 hasta","pw.plague, pw.cloud_plague"),("zombie_world","Ölüler Dünyası","500 zombi",""),
("dragon_age","Ejderha Çağı","Doğal bir ejderha uyansın","pw.spawn_dragon"),("destroyer","Yok Edici","Tek güçle 100 bina yık","pw.atomic, pw.spawn_colossus"),
("world_eater","Dünya Yiyen","Karanın %90'ını yok et","pw.antimatter, pw.black_hole"),("mad_scientist","Çılgın Bilgin","Alt tür editörüyle 10 değişiklik","pw.goo, pw.spawn_devourer"),
("gene_splicer","Gen Terzisi","5 gen sinerjisi keşfet","pw.ed_genes"),("biome_master","Biyom Ustası","Tüm normal biyomlar aynı dünyada","özel biyom tohumları"),
("star_gazer","Göğe Bakan","Yıldızbilimciler kültürü 3 krallıkta","pw.spawn_sky_visitor"),("puppet_master","Kukla Ustası","Bir krallığı 50 yıl meta kontrolle yönet","pw.meta_control"),
("hero_born","Destan","Bir birim 'Destan Kahramanı' olsun",""),("slayer","Canavar Avcısı","Tek birim 100 öldürme",""),
("old_one","Yaşlı Kurt","Bir birim 500 yaşına ulaşsın",""),("prophet","Peygamber","Plotla yeni din kurulsun",""),
("babel","Babil","Aynı anda 10 dil",""),("librarian","Kütüphaneci","Dünyada 500 kitap",""),("master_smith","Usta Demirci","Efsanevi bir eşya üretilsin",""),
("sailor","Yedi Deniz","Başka kıtaya sömürge",""),("evolution","Evrim","Bir hayvan türü medenileşsin",""),("uplift_all","Hayvan Çiftliği","5 farklı hayvan uygarlığı",""),
("frozen_world","Buz Küre","Suyun %80'i donsun",""),("fire_world","Kor Küre","Aynı anda 5.000 tile yansın",""),("possessed","Beden Değiştiren","Possession ile 50 öldürme",""),
("rebel_king","Asi Kral","İsyanla kurulan krallık başkentini alsın",""),("assassin","Gölgedeki El","Suikastle kral ölsün",""),("dynasty","Hanedan","Aynı klandan 10 kral",""),
("mixed_city","Harman Şehri","Tek şehirde 4 farklı tür",""),("undead_king","Ölü Kral","Bir ölümsüz kral olsun",""),("fairy_tale","Masal","Peri + ejderha + yürüyen ağaç aynı dünyada",""),
("all_eras","Çağlar Boyu","10 çağın hepsi doğal yaşansın",""),("apocalypse","Kıyamet","Tek yılda 3 farklı afet",""),("genesis","Yaratılış","Boş okyanustan 1.000 nüfuslu dünya",""),
("ice_to_fire","Buzdan Ateşe","Buz devrinden hemen sonra kavurucu güneş",""),("pacifist","Barış Güvercini","Barış Yemini kültürü 200 yıl",""),("heretic","Sapkın","Din bölünmesi 5 kez",""),
("collector","Koleksiyoncu","100 trait keşfet",""),("completionist","Her Şeyi Bilen","Tüm trait'leri keşfet",""),("god_named","Adım Anılsın","Tanrı heykeli 10 şehirde",""),
("colossus_war","Dev Savaşı","Mekanik Dev 1.000 birim yensin",""),("last_city","Son Kale","Dünyada zombiler 10:1 çoğunluktayken bir şehir 20 yıl dayansın",""),("cure_found","Tedavi","1.000 birim dönüşümden kurtarılsın",""),("cellular","Hücresel","Hayat Oyunu 1 şehri yok etsin","")]
save("achievements",[dict(id="ach."+a,name=b,condition=c,unlocks=d or None) for a,b,c,d in A],"Başarımlar",["id","name","condition","unlocks"])

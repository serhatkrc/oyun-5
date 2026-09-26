from lib import *
# ---------------- BUILDINGS ----------------
BL=[
# id, ad, kategori, boyut(wxh), can, maliyet, ön koşul, işlev, şehir başına maks, iş yeri
("bonfire","Kamp Ateşi","center","2x2",100,"","","Şehrin ilk çekirdeği; ısınma, toplanma",1,0),
("hall_1","Köy Meydanı","center","3x3",400,"wood:20","pop:8","Şehir merkezi; bayrak, vergi toplanır",1,0),
("hall_2","Kasaba Konağı","center","4x4",900,"wood:30|stone:30","pop:30|hall_1","Sınır yarıçapı +, lider konutu",1,0),
("hall_3","Şehir Sarayı","center","5x5",2000,"stone:60|gold:30","pop:80|hall_2","Başkent olabilir; ordu kurar",1,0),
("tent","Çadır","house","2x2",60,"wood:2|leather:2","","2 kişilik barınak",99,0),
("hut","Kulübe","house","2x2",120,"wood:6","hall_1","4 kişi",99,0),
("house","Ev","house","3x2",250,"wood:8|stone:6","hall_2","6 kişi; mutluluk +1",99,0),
("manor","Konak","house","3x3",450,"stone:14|gold:4","hall_3","10 kişi; mutluluk +2",99,0),
("storage","Ambar","economy","3x2",200,"wood:10","hall_1","Kaynak deposu; toplayıcılar buraya taşır",4,0),
("granary","Tahıl Ambarı","economy","2x3",200,"wood:8|stone:4","farm","Yiyecek bozulması -%50",3,0),
("windmill","Değirmen","economy","2x2",180,"wood:12|stone:4","granary","Buğday → un",2,1),
("bakery","Fırın","economy","2x2",160,"stone:8","windmill","Un → ekmek",3,1),
("well","Kuyu","economy","1x1",150,"stone:6","hall_1","Yangın söndürme hızı; kuraklıkta su",4,0),
("mine","Maden Ocağı","economy","3x3",300,"wood:10","hills/mountain yakın","Taş ve cevher çıkarır",3,4),
("lumber_camp","Kereste Kampı","economy","2x2",150,"wood:4","orman yakın","Odun verimi +%30",2,3),
("smithy","Demirhane","economy","2x2",250,"stone:10|iron:4","hall_2","Silah/zırh üretir",2,2),
("farm_shed","Tarla Barakası","economy","2x2",120,"wood:6","hall_1","Etrafına tarla açar",6,4),
("pasture","Ağıl","economy","4x3",120,"wood:10","evcil hayvan","Hayvan besler: süt, yün, et",3,1),
("beehive","Arı Kovanı","economy","1x1",40,"wood:2","çiçek yakın","Bal üretir",8,0),
("market","Pazar Yeri","economy","4x3",300,"wood:12|stone:8","pop:40","Ticaret kervanı, altın",1,3),
("inn","Han","social","3x3",300,"wood:14|stone:6","pop:30","Göçmen çeker, mutluluk +2",1,1),
("stable","Ahır","military","3x2",200,"wood:10","pop:40|hall_2","Binek hayvanları; süvari",1,1),
("barracks","Kışla","military","4x3",500,"stone:20|wood:10","hall_2","Asker eğitir, ordu kapasitesi",2,0),
("watchtower","Gözetleme Kulesi","military","1x1",400,"stone:10","hall_2","Menzilli otomatik savunma",8,1),
("wall","Sur","military","1x1",300,"stone:2","hall_2","Geçilmez; kuşatmada yıkılır",999,0),
("gate","Kapı","military","2x1",500,"stone:6|wood:4","wall","Dostlara açık",8,0),
("docks","İskele","naval","3x2",250,"wood:12","kıyı","Balıkçı ve nakliye gemileri",2,0),
("fishing_hut","Balıkçı Barakası","naval","2x2",120,"wood:6","kıyı","Balık verimi",4,2),
("shipyard","Tersane","naval","4x3",400,"wood:30","docks|pop:50","Savaş ve sömürge gemileri",1,2),
("library","Kütüphane","culture","3x3",350,"wood:10|stone:14","dil|pop:30","Kitap saklar; okuma",1,1),
("school","Mektep","culture","3x2",250,"wood:12","library","Çocuklara XP",1,1),
("temple","Tapınak","culture","3x3",500,"stone:20","din","Din yayılımı, rahip, büyü",1,2),
("grand_temple","Büyük Tapınak","culture","5x5",1500,"stone:60|gold:20","temple|başkent","Din merkezi; kutsal büyüler",1,4),
("healers_house","Şifahane","culture","3x2",300,"wood:10|stone:8","herbs","Hastalık iyileşmesi x2",1,2),
("graveyard","Mezarlık","culture","3x3",100,"stone:4","pop:20","Ölüler gömülür; yas süresi kısalır",1,0),
("statue_god","Yaratıcı Heykeli","monument","2x2",800,"stone:30|gold:10","hall_3","Oyuncuya adanmış; mutluluk +3 (yarıçap)",1,0),
("monument","Zafer Anıtı","monument","2x2",800,"stone:30","savaş zaferi","Sadakat +10",2,0),
("pyre","Ölü Yakma Ocağı","culture","2x2",150,"wood:10|stone:4","pop:30","Cesetleri yakar: şehirde zombi kalkışı olmaz",1,1),("nest","Yuva","house","2x2",80,"wood:2","hayvan kökenli uygarlık","Evrimleşmiş hayvanların ilk evi",99,0),
("hive","Kovan","house","3x3",200,"wood:4|honey:2","kovan zihni","Arı/karınca uygarlığı evi",99,0),
("ruins","Harabe","ruin","*",50,"","","Yıkılan binadan kalır; zamanla kaybolur",0,0),
]
save("buildings",[dict(id="bld."+a,name=b,category=c,size=d,hp=e,cost={("res."+k.split(":")[0]):int(k.split(":")[1]) for k in ids(f.replace("|",","))},
      requires=g,function=h,maxPerCity=i,jobSlots=j) for a,b,c,d,e,f,g,h,i,j in BL],"Binalar",["id","name","category","size","requires","function"])
STY=[("human","İnsan","Kerpiç/ahşap duvar, kiremit kırmızısı çatı, taş temeller","#B5533C"),
("elf","Elf","Ağaç gövdelerine oyulmuş evler, yaprak çatılar, kıvrımlı hatlar","#5C9B5A"),
("dwarf","Cüce","Kesme taş, dağa gömülü kapılar, bakır süslemeler","#7B6A5A"),
("orc","Ork","Deri gerilmiş kazık çadırlar, kemik süsler, koyu ahşap","#6B4F3A"),
("beast","Evrimleşmiş Hayvan","Toprak ve dal yuvalar; türün rengine göre tonlanır","#8A7A5A"),
("insect","Böcek Uygarlığı","Balmumu ve çamur petekler, tüneller","#C9A04A")]
save("building_styles",[dict(id="style."+a,name=b,description=c,accent=d) for a,b,c,d in STY],"Bina Stilleri",["id","name","description"])

# ---------------- JOBS ----------------
J=[("builder","İnşaatçı","Bina yapar/onarır","1 per 8 pop",""),("gatherer","Toplayıcı","Meyve, ot, mantar toplar","yiyecek < 2 yıl",""),
("farmer","Çiftçi","Tarla eker/biçer","tarla sayısına göre","bld.farm_shed"),("lumberjack","Oduncu","Ağaç keser","odun < hedef",""),
("miner","Madenci","Taş/cevher çıkarır","maden varsa","bld.mine"),("fisher","Balıkçı","Kıyıdan/tekneden balık","kıyı şehri","bld.fishing_hut"),
("hunter","Avcı","Hayvan avlar","et ihtiyacı",""),("herder","Çoban","Hayvan besler","ağıl varsa","bld.pasture"),
("smith","Demirci","Ekipman üretir","demirhane","bld.smithy"),("baker","Fırıncı","Ekmek yapar","fırın","bld.bakery"),
("trader","Tüccar","Şehirler arası ticaret","pazar","bld.market"),("warrior","Asker","Savaşır, devriye","ordu kotası","bld.barracks"),
("guard","Muhafız","Kule/kapıda nöbet","kule","bld.watchtower"),("priest","Rahip","Dua, din yayar, iyileştirir","tapınak","bld.temple"),
("scholar","Âlim","Kitap yazar, okur","kütüphane","bld.library"),("healer","Şifacı","Hastaları iyileştirir","şifahane","bld.healers_house"),
("sailor","Gemici","Gemi kullanır","tersane/iskele","bld.docks"),("leader","Şehir Lideri","Şehri yönetir","1",""),("king","Kral","Krallığı yönetir","1","")]
save("jobs",[dict(id="job."+a,name=b,task=c,quotaRule=d,requiresBuilding=e or None) for a,b,c,d,e in J],"Meslekler",["id","name","task","quotaRule"])

# ---------------- CULTURE TRAITS (77) ----------------
CU={"succession":[("succ_primogeniture","Büyük Evlat","leader:eldest_child"),("succ_election","Seçimle","leader:highest_diplo"),
 ("succ_strongest","En Güçlü","leader:highest_warfare|neuron:duel_for_crown"),("succ_wisest","En Bilge","leader:highest_intel"),
 ("succ_elders","Yaşlılar Meclisi","leader:oldest"),("succ_lot","Kura","leader:random"),("succ_matrilineal","Ana Soydan","leader:eldest_daughter"),("succ_youngest","Küçük Evlat","leader:youngest_child")],
"war":[("war_drums","Savaş Davulları","warChance:+30%|morale:+10"),("iron_fist","Demir Yumruk","rebellionResist:+40%|happiness:-1"),
 ("honor_duels","Düello Onuru","neuron:duel"),("siege_masters","Kuşatma Ustaları","wallDmg:+100%"),("shield_brothers","Kalkan Kardeşleri","armorInGroup:+5"),
 ("horse_lords","Atlı Beyler","mountedUnits:1|speed:+20%"),("archers_pride","Okçu Gururu","bowPreference:1|range:+1"),
 ("no_retreat","Geri Çekilmez","fleeThreshold:-100%"),("spare_the_weak","Zayıfa Dokunmaz","noCivilianKill:1|opinion:+10"),("raid_culture","Akıncılar","neuron:raid|lootBonus:+50%")],
"society":[("expansionists","Yayılmacılar","newCityChance:+50%"),("isolationists","İçe Kapanık","newCityChance:-50%|opinion:-10"),
 ("nomads","Göçebeler","flag:mobile_city"),("sea_folk","Deniz Halkı","colonizeChance:+60%|boatSpeed:+20%"),
 ("open_hearth","Açık Ocak","tolerance:other_species:+100%"),("pure_blood","Soy Arıklığı","tolerance:other_species:-100%"),
 ("melting_pot","Harman","mixedSpeciesHappiness:+2"),("hospitality","Misafirperver","migrantChance:+50%"),("frontier_spirit","Öncü Ruh","borderGrowth:+30%"),
 ("city_lovers","Kent Sevdalısı","maxCitySize:+30%"),("villagers","Köy Hayatı","maxCitySize:-30%|happiness:+1"),("great_walls","Sur Kültürü","buildWalls:1")],
"economy":[("farmers","Toprak Ehli","farmYield:+30%"),("harvest_feast","Hasat Şöleni","yearlyFestival:1|happiness:+2"),("hunters","Avcı Gelenekleri","huntYield:+40%"),
 ("spice_road","Baharat Yolu","tradeProfit:+40%"),("stone_carvers","Taş Oymacıları","stoneBuildings:1|buildingHp:+30%"),("artisans","Zanaatkârlar","craftQuality:+1"),
 ("merchants","Loncalar","gold:+30%"),("miners_guild","Maden Loncası","mineYield:+40%"),("fishers","Balıkçı Köyleri","fishYield:+40%"),("herders","Çobanlar","herdYield:+40%")],
"knowledge":[("scholars","Âlimler","bookWrite:+50%"),("reading_lovers","Okur Halk","readChance:+100%"),("oral_tradition","Sözlü Gelenek","xpShare:+20%|bookWrite:-50%"),
 ("ancestral_knowledge","Atalardan Bilgi","newbornGetsHalfBestParentAttr:1"),("library_keepers","Kitap Bekçileri","bookDecay:-80%"),("astronomers","Yıldızbilimciler","meteorWarning:1"),
 ("inventors","Mucitler","buildingUpgradeSpeed:+30%"),("book_burners","Kitap Yakıcılar","burnForeignBooks:1"),("mystics","Gizemciler","spellPower:+15%"),("healers_guild","Şifacılar Ocağı","diseaseCure:+40%")],
"family":[("elders_voice","Yaşlılara Hürmet","elderHappiness:+3|elderIntel:+1"),("youth_cult","Gençlik Kültü","youngXp:+30%"),("ancestor_halls","Ata Ocakları","deadMourning:-50%|faith:+10%"),
 ("large_families","Kalabalık Aileler","fertility:+30%"),("small_families","Çekirdek Aile","fertility:-20%|childStats:+10%"),("arranged_marriage","Görücü Usulü","clanStrength:+20%"),
 ("free_love","Serbest Gönül","mateChance:+30%"),("orphan_care","Yetim Koruma","childSurvival:+30%"),("twin_blessing","İkiz Uğuru","twinHappiness:+5"),("burial_rites","Defin Ritüelleri","buildGraveyard:1|happiness:+1")],
"nature":[("forest_keepers","Orman Bekçileri","treeCutting:-60%|elfOpinion:+10"),("swamp_dwellers","Bataklık Halkı","swampHappiness:+2"),
 ("animal_friends","Hayvan Dostları","huntYield:-50%|tameChance:+50%"),("tree_cutters","Baltacılar","woodYield:+40%"),("beast_tamers","Canavar Terbiyecileri","tameMonsters:1"),
 ("fire_keepers","Ateş Bekçileri","fireSpreadInCity:-60%"),("mountain_folk","Dağ Halkı","mountainHappiness:+2")],
"special":[("true_roots","Köklere Sadakat","convertResist:+100%|survivesKingdomFall:1"),("festivals","Şenlikler","monthlyFestivalChance:10%"),
 ("stoic","Metanet","happinessSwing:-50%"),("superstitious","Batıl İnançlı","eraEffects:x1.5"),("pacifist_creed","Barış Yemini","warChance:-80%"),
 ("blood_feud","Kan Davası","revengeWar:+100%"),("gift_economy","Hediye Ekonomisi","opinion:+15|gold:-20%"),("bureaucracy","Bürokrasi","loyalty:+15|buildSpeed:-10%"),
 ("tattoo_marks","Dövme Gelenekleri","fear:+10|cosmetic:tattoo"),("tower_architecture","Kule Mimarisi","towerRange:+2|cosmetic:spires"),("corpse_burners","Ölü Yakıcılar","burnCorpses:1|zombieRise:-90%")]}
save("culture_traits",[dict(id="cul."+i,name=n,group=g,effects=mods(e)) for g,l in CU.items() for i,n,e in l],"Kültür Trait'leri",["id","name","group","effects"])

# ---------------- RELIGION TRAITS (39) ----------------
RE={"deity":[("green_mother","Yeşil Ana","spell:grow|farmYield:+20%"),("serpent_coil","Yılan Halkası","onHit:poison:20"),("drowned_god","Batık Tanrı","flag:swim|boatSafety:+50%"),
 ("sun_eye","Güneş Gözü","spell:bless|immune:heat"),("deep_hammer","Derin Çekiç","craftQuality:+1|mineYield:+20%"),("frost_mother","Ayaz Ana","spell:freeze|immune:cold"),
 ("spore_choir","Spor Korosu","immune:spores|spell:poison_cloud"),("prism","Prizma","spellPower:+25%|mana:+20"),("moon_veil","Ay Peçesi","nightBonus:+25%|spell:invisibility"),
 ("hollow_king","Oyuk Kral","spell:raise_dead"),("eternal_flame","Sönmez Ateş","spell:fireball|immune:burning"),("star_choir","Yıldız Korosu","spell:meteor_call"),
 ("endless_wheel","Sonsuz Çark","onDeath:rebirth:5%"),("null","Hiçlik","spellResist:+50%|flag:no_spells"),("hive_mind","Kovan","loyalty:+25|plotChance:-50%")],
"doctrine":[("missionaries","Misyonerler","conversion:+50%"),("holy_war","Kutsal Savaş","religiousWarChance:+60%"),("pilgrimage","Hac","neuron:pilgrimage|happiness:+2"),
 ("monasticism","Manastır","priestXp:+50%|fertility:-10%"),("offerings","Adak","sacrificeFood:1|blessChance:+20%"),("fasting","Oruç","hungerRate:-10%|faith:+10%"),
 ("temple_builders","Tapınakçılar","templeBuildSpeed:+50%"),("healing_hands","Şifa Elleri","spell:heal"),("prophecy","Kehanet","disasterWarning:1"),
 ("devotion","Fedakârlık","fleeThreshold:-50%"),("paradise","Cennet İnancı","deathMourning:-50%"),("reincarnation","Ruh Göçü","xpInheritance:10%"),
 ("ancestor_worship","Ata Kültü","clanLoyalty:+20%"),("idol_makers","Put Ustaları","buildStatues:1|happiness:+1"),("iconoclasm","Put Kırıcılar","destroyForeignStatues:1"),
 ("tolerance","Hoşgörü","foreignReligionOpinion:+20"),("inquisition","Engizisyon","convertOthersForce:1|happinessOthers:-2"),("divine_kings","Tanrı Krallar","kingLoyalty:+30"),
 ("rain_dancers","Yağmur Dansçıları","spell:rain_call"),("relic_keepers","Emanet Bekçileri","legendaryItemChance:+50%"),("charity","Sadaka","poorHappiness:+2|gold:-10%"),
 ("celibate_priests","Evlenmeyen Rahipler","priestSpellPower:+30%"),("holy_animals","Kutsal Hayvanlar","huntYield:-80%|animalFriend:1"),("smiting","Göksel Ceza","spell:holy_smite"),("rot_purge","Arınma Ayini","spell:cure_rot|dmgVsUndead:+30%")]}
save("religion_traits",[dict(id="rel."+i,name=n,group=g,effects=mods(e)) for g,l in RE.items() for i,n,e in l],"Din Trait'leri",["id","name","group","effects"])

# ---------------- LANGUAGE TRAITS (25) + NAME SETS ----------------
LA=[("plain_speech","Sade Konuşma","phonology"),("soft_vowels","Yumuşak Ünlüler","phonology"),("poetic","Şiirsel","phonology"),("drumming","Davul Ritmi","phonology"),
("guttural","Gırtlaksı","phonology"),("sand_whisper","Kum Fısıltısı","phonology"),("hard_consonants","Sert Ünsüzler","phonology"),("chime","Çan Sesi","phonology"),
("hissing","Tıslamalı","phonology"),("backwards","Tersine","phonology"),("silent_signs","İşaret Dili","phonology"),("bubbling","Fokurtulu","phonology"),("buzzing","Vızıltılı","phonology"),
("complex_grammar","Karmaşık Dilbilgisi","bookPower:+30%|learnTime:+50%"),("simple_grammar","Sade Dilbilgisi","spread:+30%"),("eternal_text","Kalıcı Yazı","bookDecay:-100%"),
("confusing_semantics","Muğlak Anlam","bookPower:-20%|diplo:-1"),("many_dialects","Çok Lehçeli","splitChance:+100%"),("written_script","Yazı Sistemi","flag:can_write"),
("runic","Runik Yazı","flag:can_write|enchantChance:+10%"),("pictographic","Resim Yazısı","flag:can_write|bookWrite:-30%"),("loanwords","Alıntı Sözcükler","adoptWords:1"),
("sacred_tongue","Kutsal Dil","faith:+20%"),("trade_tongue","Ticaret Dili","tradeProfit:+20%|spread:+20%"),("whistled","Islık Dili","commRange:+100%")]
save("language_traits",[dict(id="lang."+a,name=b,group=("phonology" if c=="phonology" else "grammar"),effects=({} if c=="phonology" else mods(c)))
      for a,b,c in LA],"Dil Trait'leri",["id","name","group","effects"])
NS={
"plain_speech":dict(onset="b,d,g,k,l,m,n,r,s,t,v,y",vowel="a,e,i,o,u",coda="n,r,l,s,t,",pattern="CV,CVC,CVCV,CVCVC"),
"soft_vowels":dict(onset="l,n,m,s,th,f,v,y,r",vowel="a,e,i,ae,ia,ie,o",coda="l,n,s,th,",pattern="CV,CVV,CVCV,VCVCV"),
"poetic":dict(onset="l,m,n,r,s,v,f,sh",vowel="a,e,i,o,ea,ou",coda="n,l,r,",pattern="CVCV,CVCVCV,VCVC"),
"drumming":dict(onset="b,d,k,m,n,t,mb,nd",vowel="a,u,o,i",coda=",m,n",pattern="CVCV,CVCVCV,CV"),
"guttural":dict(onset="g,gr,k,kr,dr,br,z,zg,h",vowel="a,o,u,aa",coda="k,g,rk,z,sh,",pattern="CVC,CVCC,CVCVC"),
"sand_whisper":dict(onset="s,z,h,k,r,f,m,sh",vowel="a,i,aa,ii,u",coda="r,s,m,n,",pattern="CVCV,CVCVC,CV"),
"hard_consonants":dict(onset="b,d,g,k,t,dr,gr,th,br",vowel="a,o,u,i",coda="k,rn,nd,rk,m,",pattern="CVC,CVCVC,CVCC"),
"chime":dict(onset="l,t,n,s,r,y",vowel="i,ee,ai,e,a",coda="n,l,",pattern="CV,CVCV,CVCVCV"),
"hissing":dict(onset="s,ss,z,sh,x,t,k",vowel="a,i,e,ia",coda="s,ss,x,sh,",pattern="CVC,CVCVC,CVCCV"),
"backwards":dict(onset="n,r,t,l,m,k",vowel="a,e,o,u",coda="n,r,t,",pattern="CVC,CVCVC",note="Üretilen ismin tersi kullanılır"),
"silent_signs":dict(onset="",vowel="",coda="",pattern="SYMBOL",note="İsimler sembol+sayı: '◇7', '△Üç'"),
"bubbling":dict(onset="b,p,bl,gl,w,m,l",vowel="u,o,oo,ou,a",coda="b,p,l,",pattern="CV,CVCV,CVCVCV"),
"buzzing":dict(onset="z,zz,v,b,dz",vowel="i,e,a,ee",coda="z,zz,",pattern="CVC,CVCVC")}
open(OUT+"/name_sets.json","w",encoding="utf-8",newline="\n").write(json.dumps({"rules":{
 "unit":"1–2 kelime; kelime = pattern'den seçilen şablon (C=onset, V=vowel, sonra %40 coda)","city":"kelime + (%30) şehir eki: 'ia','heim','gar','ova'",
 "kingdom":"'{kök} {unvan}' veya '{kök}ya'; unvan krallık büyüklüğüne göre","clan":"'{kök}oğulları' benzeri soy eki","culture":"'{kök}'lı","religion":"'{kök} Yolu' / '{kök} Tarikatı'",
 "book":"şablon: '{kök}'in {Tür}'u'"},"sets":{("lang."+k):v for k,v in NS.items()},
 "rulerTitles":[{"minCities":1,"title":"Reis"},{"minCities":3,"title":"Bey"},{"minCities":6,"title":"Kral"},{"minCities":12,"title":"Hükümdar"},{"minCities":25,"title":"İmparator"}]},ensure_ascii=False,indent=1))

# ---------------- CLAN TRAITS (28) ----------------
CL=[("iron_blood","Demir Kan","hp:+10%"),("wise_line","Bilgeler Soyu","intel:+1"),("cursed_line","Lanetli Soy","luck:-10|traitChance:cursed"),
("royal_blood","Kraliyet Kanı","leaderChance:+50%"),("long_lived_line","Uzun Ömürlüler","lifespan:+20%"),("fertile_line","Bereketli Ocak","fertility:+25%"),
("hunter_line","Avcılar","huntYield:+30%"),("sea_line","Denizciler","boatSpeed:+20%"),("smith_line","Demirciler","craftQuality:+1"),
("mage_line","Büyücüler","mana:+30|spellChance:+20%"),("healer_line","Şifacılar","traitChance:healer"),("traitor_line","Hainler Soyu","loyalty:-20"),
("loyal_line","Sadıklar","loyalty:+20"),("schemer_line","Entrikacılar","plotChance:+50%"),("peacemaker_line","Arabulucular","diplo:+2"),
("zealot_line","Bağnazlar","faith:+50%"),("beauty_line","Güzeller","traitChance:attractive"),("giant_line","İriler","traitChance:giant"),
("swift_line","Çevikler","speed:+10%"),("lucky_line","Talihliler","luck:+10"),("unlucky_line","Kara Bahtlılar","luck:-10"),
("feud_line","Kan Davalılar","revengeChance:+100%"),("founder_line","Kurucu Soy","newCityLeaderChance:+50%"),("nomad_line","Göçerler","migrateChance:+50%"),
("scholar_line","Kalemşorlar","bookWrite:+40%"),("beast_line","Canavar Kanı","dmg:+10%|diplo:-1"),("shadow_line","Gölgeler","stealth:+1|plotSpeed:+30%"),("blessed_line","Kutlular","traitChance:blessed")]
save("clan_traits",[dict(id="clan."+a,name=b,effects=mods(c)) for a,b,c in CL],"Klan Trait'leri",["id","name","effects"])
KT=[("seafaring","Denizci Krallık","colonizeChance:+50%"),("militarist","Militarist","armySize:+30%"),("mercantile","Tüccar Krallık","gold:+30%"),
("theocracy","Teokrasi","faith:+30%|templeBonus:1"),("isolationist","Yalnızcı","opinion:-15|rebellionResist:+20%"),("conquerors","Fetihçi","warChance:+40%"),
("scholarly","Bilgin Krallık","bookWrite:+30%"),("fortress","Kale Krallığı","wallHp:+50%")]
save("kingdom_traits",[dict(id="kt."+a,name=b,effects=mods(c)) for a,b,c in KT],"Krallık Trait'leri",["id","name","effects"])

# ---------------- PLOTS ----------------
PL=[("rebellion","İsyan","city_leader","loyalty<30|ambitious_or_inspired","3",24,"Şehir bağımsız krallık olur"),
("new_religion","Yeni Din Kurma","any","intel>=6|abstract_thought|zealous_or_inspired","2",36,"Yeni din; kurucu peygamber olur"),
("culture_split","Kültür Ayrılığı","leader","intel>=6|2 civil>=2|ambitious_or_inspired","3",36,"Kültür ikiye bölünür"),
("language_split","Lehçe Ayrılığı","any","dil 3+ şehre yayılmış|uzak şehir","2",48,"Yeni dil doğar"),
("alliance","İttifak Kurma","king","diplo>=6|ortak düşman","1",18,"İttifak meta nesnesi"),
("war_declaration","Savaş Kışkırtma","king_or_general","warfare>=5|opinion<-20","2",12,"Savaş ilanı"),
("assassination","Suikast","any","deceitful_or_cruel|hedef lider","2",12,"Hedef ölür; yakalanırsa infaz"),
("usurpation","Taht Gaspı","noble","ambitious|warfare>=6","4",24,"Kral devrilir"),
("conversion","Din Değiştirme","priest","kendi şehri","1",12,"Şehir halkı plotçunun dinine döner (sadece kendi şehri)"),
("new_clan","Klan Kurma","any","ambitious|çocuk>=3","1",12,"Yeni klan"),
("secession","Bağımsızlık","city_leader","farklı kültür/din|loyalty<40","3",24,"Birden fazla şehir yeni krallık"),
("royal_marriage","Kraliyet Evliliği","king","opinion>10","1",6,"Fikir +30, ittifak şansı"),
("reform","Reform","king","intel>=7","2",36,"Kültür trait'lerinden biri değişir"),
("crusade","Kutsal Sefer","high_priest","holy_war doktrini","3",18,"Farklı dinli krallığa din savaşı"),
("peace_treaty","Barış Antlaşması","king","savaş>=5 yıl|diplo>=5","1",6,"Savaş biter"),
("dark_ritual","Karanlık Ritüel","any","cultist_or_cursed|mage_blood","3",12,"İblis çağırma veya ölü diriltme felaketi")]
save("plots",[dict(id="plot."+a,name=b,initiator=c,conditions=d.split("|"),minParticipants=int(e),durationMonths=f,outcome=g) for a,b,c,d,e,f,g in PL],
     "Plotlar (Entrikalar)",["id","name","initiator","durationMonths","outcome"])

WT=[("conquest","Fetih Savaşı"),("rebellion","İsyan Savaşı"),("religious","Din Savaşı"),("independence","Bağımsızlık Savaşı"),
("revenge","İntikam Savaşı"),("succession","Veraset Savaşı"),("world_war","Herkese Karşı")]
save("war_types",[dict(id="war."+a,name=b) for a,b in WT],"Savaş Türleri",["id","name"])
BK=[("history","Tarih",["intel:+1"]),("poetry","Şiir",["happiness:+2"]),("science","İlim",["intel:+2"]),("warfare","Savaş Sanatı",["warfare:+1"]),
("scripture","Kutsal Metin",["faith:+20%"]),("law","Kanunname",["steward:+1"]),("medicine","Tıp",["diseaseResist:+10%"]),("craft","Zanaat",["craftQuality:+0.5"]),
("travel","Seyahatname",["speed:+3%"]),("forbidden","Yasak Bilgi",["mana:+20","happiness:-2"])]
save("book_types",[dict(id="book."+a,name=b,readerBonus=c) for a,b,c in BK],"Kitap Türleri",["id","name","readerBonus"])
HE=[("new_home","Yeni ev",4),("festival","Şenlik",5),("married","Evlilik",6),("child_born","Çocuk doğdu",5),("ate_well","İyi yemek",1),
("hungry","Açlık",-3),("homeless","Evsizlik",-4),("family_died","Aile üyesi öldü",-8),("friend_died","Arkadaşı öldü",-4),("war_won","Savaş zaferi",4),
("war_lost","Savaş yenilgisi",-5),("city_captured","Şehri ele geçirildi",-10),("king_died","Kral öldü",-3),("plague_nearby","Yakında salgın",-3),
("read_book","Kitap okudu",2),("prayed","Dua etti",1),("new_king_liked","Sevilen kral",3),("heavy_tax","Ağır vergi",-2),("disaster","Afet yaşadı",-6),
("blessed_by_god","Tanrı kutsadı",6),("cursed_by_god","Tanrı lanetledi",-6),("foreign_rule","Yabancı yönetim",-3),("same_faith_king","Aynı dinden kral",2),
("promoted","Terfi",3),("gift_received","Hediye aldı",2),("insulted","Hakarete uğradı",-2),("won_duel","Düello kazandı",4),("saw_miracle","Mucize gördü",5),
("statue_nearby","Heykel yakında",1),("era_change","Çağ değişimi",0)]
save("happiness_events",[dict(id="hap."+a,name=b,value=c) for a,b,c in HE],"Mutluluk Olayları",["id","name","value"])

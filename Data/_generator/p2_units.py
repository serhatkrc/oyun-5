from lib import *

# ---------------- SPECIES ----------------
# kategori: civ=başlangıçtan medeni, evo=evrimleşebilir hayvan, animal=evrimleşemez hayvan, monster, undead, elemental, magic, special
S = [
# id, ad, kat, diyet, habitat, can, hasar, zırh, hız, ömür, boy(1-5), üreme, yetenekler, not
("human","İnsan","civ","omni","land",100,12,0,1.0,70,2,"live","","Dengeli; her biyoma uyum sağlar, hızlı çoğalır"),
("elf","Elf","civ","herb","land",90,11,0,1.15,300,2,"live","spell:heal","Uzun ömürlü, iyi okçu, yavaş çoğalır"),
("dwarf","Cüce","civ","omni","land",130,13,5,0.85,200,2,"live","","Madenci ve demirci; dağlara yerleşir"),
("orc","Ork","civ","carn","land",140,16,2,1.0,60,3,"live","","Savaşçı; çok hızlı çoğalır, saldırgan"),
# evrimleşebilir hayvanlar
("wolf","Kurt","evo","carn","land",60,10,0,1.3,20,2,"live","","Sürü halinde avlanır"),
("bear","Ayı","evo","omni","land",180,20,3,0.9,30,4,"live","","Bal ve balık sever"),
("cat","Kedi","evo","carn","land",30,6,0,1.4,18,1,"live","","Fare avcısı"),
("dog","Köpek","evo","carn","land",45,8,0,1.3,15,1,"live","","Medenilere evcilleşir"),
("rabbit","Tavşan","evo","herb","land",15,1,0,1.6,8,1,"live","","Çok hızlı çoğalır"),
("sheep","Koyun","evo","herb","land",40,2,0,0.9,12,2,"live","","Yün verir"),
("cow","İnek","evo","herb","land",90,4,1,0.8,20,3,"live","","Süt ve et"),
("chicken","Tavuk","evo","omni","land",12,1,0,1.0,8,1,"egg","","Yumurta"),
("frog","Kurbağa","evo","carn","amph",20,3,0,1.2,10,1,"egg","","Böcek yer; bataklıkta çoğalır"),
("rat","Sıçan","evo","omni","land",12,2,0,1.4,4,1,"live","","Veba taşır"),
("monkey","Maymun","evo","herb","land",40,5,0,1.4,25,2,"live","flag:climb","Zeki; evrime yatkın"),
("penguin","Penguen","evo","carn","amph",30,3,0,0.9,20,1,"egg","","Soğukta yaşar, iyi yüzer"),
("fox","Tilki","evo","carn","land",30,6,0,1.5,12,1,"live","","Kurnaz avcı"),
("crab","Yengeç","evo","omni","amph",35,5,6,0.8,15,1,"egg","","Kabuklu"),
("scorpion","Akrep","evo","carn","land",30,8,4,1.0,10,1,"egg","grant:venom","Zehirli iğne"),
("lizard","Kertenkele","evo","carn","land",25,4,2,1.3,12,1,"egg","","Sıcağa dayanıklı"),
("crocodile","Timsah","evo","carn","amph",160,22,6,0.8,60,4,"egg","","Pusu avcısı"),
("bee","Arı","evo","herb","air",5,2,0,1.5,2,1,"egg","grant:venom","Kovan kurar; bal üretir"),
("ant","Karınca","evo","omni","land",6,2,1,1.2,3,1,"egg","","Koloni halinde çalışır"),
("beetle","Böcek","evo","herb","land",20,3,5,0.8,5,1,"egg","","Sert kabuk"),
("snake","Yılan","evo","carn","land",25,7,0,1.1,15,1,"egg","grant:venom",""),
("turtle","Kaplumbağa","evo","herb","amph",80,3,12,0.4,120,2,"egg","","Çok uzun ömür"),
("deer","Geyik","evo","herb","land",50,5,0,1.5,15,2,"live","",""),
("boar","Yaban Domuzu","evo","omni","land",70,9,2,1.2,15,2,"live","",""),
("goat","Keçi","evo","herb","land",40,5,0,1.2,15,2,"live","grant:climber","Dağlara tırmanır"),
("owl","Baykuş","evo","carn","air",20,4,0,1.3,20,1,"egg","grant:night_owl",""),
("crow","Karga","evo","omni","air",12,2,0,1.4,15,1,"egg","","Zeki; ölüleri izler"),
("eagle","Kartal","evo","carn","air",30,8,0,1.8,25,2,"egg","",""),
("parrot","Papağan","evo","herb","air",12,2,0,1.5,40,1,"egg","","Taklitçi; dil öğrenmeye yatkın"),
("seal","Fok","evo","carn","amph",70,6,2,0.7,25,2,"live","",""),
("hyena","Sırtlan","evo","carn","land",55,9,0,1.4,15,2,"live","",""),
("lion","Aslan","evo","carn","land",120,18,1,1.4,15,3,"live","",""),
("rhino","Gergedan","evo","herb","land",220,20,10,0.9,40,4,"live","","Hücum eder"),
("buffalo","Manda","evo","herb","land",150,12,4,0.9,20,3,"live","",""),
("camel","Deve","evo","herb","land",100,5,1,1.0,40,3,"live","grant:heatproof",""),
("reindeer","Ren Geyiği","evo","herb","land",70,6,1,1.3,15,2,"live","grant:coldproof",""),
("polar_bear","Kutup Ayısı","evo","carn","amph",200,22,4,0.9,25,4,"live","grant:coldproof",""),
("snail","Salyangoz","evo","herb","land",15,1,6,0.2,5,1,"egg","",""),
("salamander","Semender","evo","carn","land",40,6,2,1.0,20,1,"egg","grant:heatproof","Ateşte yaşar"),
("horse_wild","Yaban Atı","evo","herb","land",90,6,0,1.9,25,3,"live","","Evcilleşince binek"),
# evrimleşemez hayvanlar
("fish","Balık","animal","herb","water",8,0,0,1.2,4,1,"egg","","Besin kaynağı"),
("butterfly","Kelebek","animal","herb","air",3,0,0,1.0,1,1,"meta","","Tırtıldan dönüşür"),
("caterpillar","Tırtıl","animal","herb","land",5,0,0,0.3,1,1,"egg","","Metamorfoz ile kelebeğe"),
("piranha","Pirana","animal","carn","water",10,6,0,1.6,5,1,"egg","","Suya düşeni parçalar"),
("mosquito_swarm","Sivrisinek Sürüsü","animal","carn","air",8,1,0,1.4,1,1,"egg","grant:plague_bearer","Hastalık yayar"),
# canavarlar
("dragon","Ejderha","monster","carn","air",3000,120,40,1.2,1000,5,"egg","grant:fire_breath|grant:flight","Şehirleri yakar, altın biriktirir"),
("sandworm","Kum Kurdu","monster","carn","under",1500,60,20,1.0,300,5,"split","","Yerin altından çıkıp yutar"),
("kraken","Derin Canavarı","monster","carn","water",2000,70,20,0.8,500,5,"egg","","Gemileri batırır"),
("ember_imp","Kor İblisi","monster","carn","land",90,14,2,1.2,100,2,"summon","grant:fire_breath|grant:heatproof","Yarıklardan çıkar"),
("rot_crawler","Çürük Sürüngen","monster","carn","land",60,10,2,1.0,40,2,"split","grant:plague_bearer",""),
("candy_golem","Şeker Golemi","monster","herb","land",250,15,15,0.6,200,3,"split","",""),
("gummy_bear","Jöle Ayı","monster","herb","land",60,5,5,1.0,20,2,"split","",""),
("shroomling","Mantarcık","monster","herb","land",40,5,0,0.9,15,1,"spore","","Spor hastalığıyla dönüşenler"),
("crystal_beetle","Kristal Böcek","monster","herb","land",60,6,15,0.8,30,1,"egg","",""),
("ash_crawler","Kül Sürüngeni","monster","carn","land",70,11,4,1.0,30,2,"egg","",""),
("bone_crawler","Kemik Örümceği","monster","carn","land",80,12,5,1.2,50,2,"egg","",""),
("clock_crab","Saat Yengeci","monster","herb","amph",50,6,10,0.8,999,1,"egg","","Yaşlanmaz"),
("void_moth","Boşluk Güvesi","monster","herb","air",20,3,0,1.3,10,1,"egg","","Manayı emer"),
("flesh_mound","Et Yığını","monster","carn","land",200,15,0,0.5,50,3,"tumor","grant:regeneration","Tümör hastalığından doğar"),
("devourer","Yutucu Kütle","monster","omni","land",150,12,5,0.7,999,3,"assimilate","","Değdiği birimi kendine dönüştürür"),
("slime","Balçık","monster","omni","land",40,5,0,0.6,30,1,"split","","İkiye bölünerek çoğalır"),
("walking_tree","Yürüyen Ağaç","magic","herb","land",400,25,15,0.4,800,4,"seed","","Ormanları korur"),
("fairy","Peri","magic","herb","air",20,3,0,1.6,500,1,"egg","spell:heal|spell:bless","Çiçekleri büyütür"),
("sky_whale_calf","Gök Balinası Yavrusu","magic","herb","air",600,5,5,0.5,300,5,"live","","Gökte süzülür"),
# ölümsüzler
("skeleton","İskelet","undead","none","land",60,10,2,1.0,0,2,"summon","immune:plague","Ölü diriltmeyle doğar"),
("zombie","Zombi","undead","brains","land",80,10,0,0.6,0,2,"infect","","Isırdığını dönüştürür"),
("zombie_runner","Koşucu Zombi","undead","brains","land",55,9,0,1.5,0,2,"infect","","Taze dönüşmüş; hızlı ama çabuk çürür (ömür 3 yıl)"),
("zombie_brute","İri Zombi","undead","brains","land",320,26,6,0.5,0,4,"infect","","İri birimlerden (ork, ayı, dev) dönüşür; kapıları kırar"),
("zombie_bloater","Şişkin Zombi","undead","brains","land",120,4,0,0.4,0,3,"infect","","Ölünce patlar; çevresine çürük bulutu yayar"),
("zombie_crawler","Sürünen Zombi","undead","brains","land",30,6,0,0.3,0,1,"infect","","Bacaksız; pusuda bekler, yüzebilir"),
("zombie_beast","Zombi Hayvan","undead","brains","land",0,0,0,0,0,0,"infect","","Enfekte hayvan: kendi türünün istatistikleri x0.8, hız x0.7, çürük paleti"),
("zombie_dragon","Çürük Ejderha","undead","brains","air",2500,90,30,1.0,0,5,"infect","grant:flight","Ölen ejderha dönüşürse; ateş yerine çürük nefes"),
("zombie_lord","Zombi Efendisi","undead","brains","land",600,30,8,0.9,0,3,"none","spell:raise_dead|spell:rot_breath","Büyük sürülerde 500 zombi başına bir tane doğar; sürüyü yönetir"),
("ghost","Hayalet","undead","none","air",50,8,0,1.2,0,2,"summon","flag:phase","Duvarlardan geçer"),
("necromancer","Ölü Çağırıcı","undead","omni","land",150,10,2,1.0,300,2,"none","spell:raise_dead","Mezarlıklarda güçlenir"),
("bloodsucker","Kan Emici","undead","blood","land",140,16,2,1.3,0,2,"infect","grant:vampiric","Sarımsaktan kaçar"),
# elementaller
("fire_elemental","Ateş Elementali","elemental","none","land",120,18,0,1.2,0,2,"none","grant:heatproof","Soğuk çağlarda erir"),
("water_elemental","Su Elementali","elemental","none","amph",140,12,0,1.0,0,2,"none","","Yangın söndürür"),
("earth_golem","Toprak Golemi","elemental","none","land",400,30,25,0.5,0,4,"none","","Yavaş ama durdurulamaz"),
("snowman","Kardan Adam","elemental","none","land",40,4,0,0.8,0,2,"none","","Sıcakta erir"),
("storm_spirit","Fırtına Ruhu","elemental","none","air",90,20,0,1.6,0,2,"none","spell:lightning",""),
# büyücüler
("dark_mage","Kara Büyücü","magic","omni","land",120,8,0,1.0,200,2,"none","spell:fireball|spell:curse","Köyleri yakar"),
("white_mage","Ak Büyücü","magic","omni","land",120,8,0,1.0,200,2,"none","spell:heal|spell:shield","Yaralıları iyileştirir"),
("druid","Druid","magic","herb","land",110,8,0,1.0,250,2,"none","spell:grow|spell:entangle",""),
# özel
("sky_visitor","Gök Ziyaretçisi","special","none","air",200,20,10,1.8,999,2,"none","spell:abduct","Gemilerle iner"),
("colossus","Mekanik Dev","special","none","land",5000,150,60,0.8,0,5,"none","","Oyuncunun yönetebildiği dev savaş makinesi"),
("bandit","Haydut","special","omni","land",100,14,2,1.1,60,2,"live","","Kanunsuz insanlar; köy yağmalar"),
("cultist","Tarikatçı","special","omni","land",90,9,0,1.0,60,2,"live","spell:summon_imp","Karanlık ritüeller"),
]
sp=[]
for s in S:
    (i,n,c,d,h,hp,dm,ar,spd,life,size,rep,ab,note)=s
    sp.append(dict(id="sp."+i,name=n,category=c,sapientAtStart=(c=="civ"),canEvolve=(c in("evo","civ")),
      diet=d,habitat=h,stats=dict(hp=hp,damage=dm,armor=ar,speed=spd,lifespan=life,size=size),
      reproduction=rep,abilities=mods(ab),note=note))
save("species",sp,"Türler ve Yaratıklar",["id","name","category","diet","habitat","reproduction","note"])

# ---------------- UNIT TRAITS (115) ----------------
# rarity: N=normal R=nadir E=epik L=efsanevi ; kalıtım %
U = [
# --- Beden
("strong","Güçlü","body","N","dmg:+20%","weak",40),("weak","Cılız","body","N","dmg:-20%","strong",30),
("swift","Çevik","body","N","speed:+20%","sluggish",35),("sluggish","Hantal","body","N","speed:-20%","swift",30),
("giant","Dev Cüsse","body","R","hp:+50%|size:+1|speed:-10%","tiny",30),("tiny","Ufak Tefek","body","N","hp:-25%|dodge:+10|size:-1","giant",30),
("tough","Dayanıklı","body","N","hp:+25%","fragile",35),("fragile","Kırılgan","body","N","hp:-25%","tough",30),
("thick_skin","Kalın Deri","body","N","armor:+10","",35),("agile","Sıçrayışlı","body","N","dodge:+12","",30),
("long_life","Uzun Ömürlü","body","R","lifespan:+50%","short_life",45),("short_life","Kısa Ömürlü","body","N","lifespan:-40%","long_life",40),
("fertile","Doğurgan","body","N","fertility:+40%","infertile",40),("infertile","Kısır","body","N","fertility:-100%","fertile",0),
("attractive","Alımlı","body","N","mateChance:+30%|diplo:+1","ugly",30),("ugly","Çirkin","body","N","mateChance:-30%","attractive",30),
# --- Zihin
("genius","Dâhi","mind","R","intel:+5|xp:+30%","dim",25),("dim","Kalın Kafalı","mind","N","intel:-3|xp:-20%","genius",25),
("wise","Bilge","mind","R","intel:+3|diplo:+2","",25),("curious","Meraklı","mind","N","xp:+10%|neuron:explore","",20),
("forgetful","Unutkan","mind","N","xp:-15%","",20),("strategist","Stratejist","mind","R","warfare:+4","",20),
("orator","Hatip","mind","R","diplo:+4","",20),("steward","Kâhya","mind","R","steward:+4","",20),
("bookworm","Kitap Kurdu","mind","N","readBonus:+100%|neuron:read_book","illiterate",15),("illiterate","Okuma Bilmez","mind","N","readBonus:-100%","bookworm",15),
("fast_learner","Çabuk Kavrayan","mind","N","xp:+25%","",20),("stubborn","İnatçı","mind","N","convertResist:+50%","",25),
# --- Karakter
("ambitious","Hırslı","char","N","plotChance:+50%|warfare:+1","content",20),("content","Kanaatkâr","char","N","happiness:+2|plotChance:-50%","ambitious",20),
("peaceful","Barışsever","char","N","warChance:-50%|opinion:+10","bloodthirsty",20),("bloodthirsty","Kana Susamış","char","N","dmg:+10%|warChance:+50%","peaceful",20),
("greedy","Açgözlü","char","N","taxRate:+20%|loyalty:-5","generous",20),("generous","Cömert","char","N","loyalty:+10|opinion:+5","greedy",20),
("honest","Dürüst","char","N","opinion:+10","deceitful",15),("deceitful","Hilekâr","char","N","plotSpeed:+30%|opinion:-10","honest",15),
("brave","Yürekli","char","N","fleeThreshold:-50%","coward",20),("coward","Korkak","char","N","fleeThreshold:+100%","brave",20),
("loyal","Sadık","char","N","loyalty:+15","treacherous",20),("treacherous","Hain","char","N","loyalty:-15|plotChance:+30%","loyal",20),
("cruel","Zalim","char","N","fear:+20|happinessOthers:-2","merciful",20),("merciful","Merhametli","char","N","spareEnemy:+50%","cruel",20),
("zealous","Bağnaz","char","N","faith:+50%|tolerance:-50%","skeptic",25),("skeptic","Şüpheci","char","N","faith:-50%","zealous",25),
("cheerful","Neşeli","char","N","happiness:+3","gloomy",25),("gloomy","Karamsar","char","N","happiness:-3","cheerful",25),
("romantic","Âşık Ruhlu","char","N","mateChance:+50%","loner",20),("loner","Yalnız Kurt","char","N","neuron:wander|mateChance:-40%","romantic",20),
("wanderer","Gezgin","char","N","neuron:migrate|speed:+5%","homebody",15),("homebody","Ev Kuşu","char","N","happiness:+2|neuron:stay_home","wanderer",15),
# --- Beceri
("eagle_eye","Şahin Gözü","skill","N","range:+2|crit:+10","",25),("blademaster","Kılıç Ustası","skill","R","dmg:+15%|atkspd:+15%","",20),
("shield_wall","Kalkan Duvarı","skill","N","armor:+8|block:+15","",20),("master_builder","Usta Eller","skill","N","buildSpeed:+50%","",20),
("green_thumb","Yeşil Parmak","skill","N","farmYield:+50%","",20),("miner","Maden Burnu","skill","N","mineYield:+50%","",20),
("angler","Olta Ustası","skill","N","fishYield:+50%","",20),("hunter","İzci","skill","N","huntYield:+50%|speed:+5%","",25),
("sailor","Denizci","skill","N","boatSpeed:+30%","",20),("smith","Demir Döven","skill","R","craftQuality:+1","",20),
("healer","Şifacı","skill","R","spell:heal","",15),("trader","Tüccar Ruhu","skill","N","tradeProfit:+30%","",20),
("runner","Maratoncu","skill","N","stamina:+50%|speed:+10%","",25),("swimmer","Balık Gibi","skill","N","flag:swim|swimSpeed:+50%","",30),
("climber","Dağ Keçisi","skill","N","mountainCost:-60%","",30),("iron_stomach","Demir Mide","skill","N","hungerRate:-30%|immune:food_poison","glutton",25),
("glutton","Obur","skill","N","hungerRate:+40%","iron_stomach",25),("night_owl","Gece Kuşu","skill","N","nightBonus:+20%","",20),
# --- Güç/büyü
("fire_breath","Ateş Soluğu","power","E","spell:fire_breath|immune:burning","",15),("frost_touch","Buz Dokunuşu","power","E","onHit:freeze:10","",15),
("venom","Zehirli Isırık","power","R","onHit:poison:30","",30),("regeneration","Yenilenme","power","E","regen:+2","",20),
("immortal","Ölümsüz","power","L","lifespan:0|flag:no_aging","",5),("flight","Kanatlı","power","E","flag:fly","",25),
("teleporter","Işınlanan","power","E","spell:teleport|neuron:teleport_home","",10),("stormcaller","Şimşek Çağıran","power","E","spell:lightning","",10),
("necro","Ölü Fısıltısı","power","E","spell:raise_dead","",10),("mage_blood","Büyü Kanı","power","R","mana:+50|manaRegen:+50%","",25),
("holy","Kutsal Işık","power","E","spell:heal|dmgVsUndead:+100%","",10),("shadowstep","Gölge Adımı","power","E","dodge:+25|stealth:+1","",10),
("earthshaker","Yer Sarsan","power","E","spell:quake_stomp","",10),("vampiric","Kan Emici","power","E","lifesteal:+20%|weak:sunlight|weak:garlic","",0),
("thorns","Diken Kabuk","power","R","reflect:+15%","",25),("volatile","Patlak","power","R","onDeath:explode:small","",15),
("berserker","Cinnet Öfkesi","power","R","lowHpDmg:+50%","",15),("courage_aura","Cesaret Aurası","power","E","aura:morale:+20","",10),
("blessed","Kutsanmış","power","R","luck:+10|hp:+10%","cursed",10),("cursed","Lanetli","power","R","luck:-10|happiness:-2","blessed",10),
# --- Durumlar/olumsuz
("blind","Kör","cond","N","range:-3|crit:-20","eagle_eye",5),("lame","Topal","cond","N","speed:-35%","",0),
("one_eye","Tek Göz","cond","N","range:-1","",0),("deaf","Sağır","cond","N","ambushResist:-30%","",5),
("sickly","Hastalıklı","cond","N","diseaseResist:-50%","immune",20),("mad","Deli","cond","N","flag:attack_all","",0),
("scarred","Yara İzli","cond","N","fear:+10","",0),("sleepless","Uykusuz","cond","N","sleepNeed:-50%|happiness:-1","",15),
("weak_immunity","Zayıf Bağışıklık","cond","N","diseaseResist:-30%","immune",25),("immune","Bağışık","cond","R","immune:plague|immune:infection","sickly",30),
("heatproof","Ateşe Dayanıklı","cond","N","immune:heat|burnDmg:-70%","",30),("coldproof","Soğuğa Dayanıklı","cond","N","immune:cold","",30),
("poisonproof","Zehre Dayanıklı","cond","N","immune:poison","",30),("waterborn","Su Doğumlu","cond","R","flag:breathe_water|flag:swim","",40),
# --- Efsanevi / kazanılan
("chosen_one","Seçilmiş","legend","L","allStats:+30%|luck:+20","",0),("hero","Destan Kahramanı","legend","L","dmg:+30%|hp:+30%|aura:morale:+30","",0),
("divine_touch","İlahi Dokunuş","legend","L","flag:edited","",0),("plague_bearer","Salgın Taşıyıcı","legend","R","carry:plague|immune:plague","",10),
("undead_curse","Ölümsüz Laneti","legend","E","onDeath:rise_zombie","",0),("golden_heart","Altın Yürek","legend","E","happinessOthers:+3|loyalty:+20","",5),
("kingslayer","Kral Katili","legend","E","fear:+30|warfare:+2","",0),("survivor","Hayatta Kalan","legend","R","hp:+15%|fleeSuccess:+30%","",5),
("veteran","Kıdemli","legend","R","dmg:+10%|armor:+5","",0),("lucky","Şanslı","legend","R","luck:+15","unlucky",20),
("unlucky","Talihsiz","legend","N","luck:-15","lucky",20),("moonborn","Ay Çocuğu","legend","E","nightBonus:+40%|mana:+30","",15),
("stormborn","Fırtına Doğumlu","legend","E","immune:lightning|speed:+15%","",15),
("zombie_hunter","Ölü Avcısı","skill","R","dmgVsUndead:+60%|infectResist:+30%","",10),("rot_resistant","Çürüğe Dirençli","cond","R","infectResist:+60%","",30),
("hollow","Oyuk","cond","N","flag:undead|flag:no_needs|happiness:0","",0),
]
ut=[dict(id="tr."+a,name=b,group=c,rarity={"N":"normal","R":"rare","E":"epic","L":"legendary"}[d],effects=mods(e),
    opposite=("tr."+f) if f else None,inheritChance=g/100) for a,b,c,d,e,f,g in U]
save("unit_traits",ut,"Birim Trait'leri",["id","name","group","rarity","effects","opposite"])

# ---------------- STATUS EFFECTS ----------------
ST=[("burning","Yanıyor",10,"hp:-3/tick","Suya girince/yağmurda biter; yanabilir tile'ı tutuşturur"),
("frozen","Donmuş",8,"flag:stunned","Hareket ve saldırı yok; ateş hasarı çözer"),
("poisoned","Zehirlenmiş",15,"hp:-1/tick","Şifacı ve kutsal ışık temizler"),("slowed","Yavaşlamış",6,"speed:-50%",""),
("stunned","Sersem",3,"flag:stunned",""),("shielded","Kalkanlı",20,"dmgTaken:-60%","Büyü kalkanı"),
("sleeping","Uyuyor",0,"flag:asleep","Enerji dolar; saldırıya açık"),("pregnant","Hamile",12,"speed:-20%","Süre sonunda doğum"),
("in_egg","Yumurtada",10,"flag:immobile","Yumurta kırılınca yavru"),("cocoon","Kozada",15,"flag:immobile","Metamorfoz"),
("blessed","Kutsanmış",30,"luck:+20|regen:+1",""),("cursed","Lanetli",30,"luck:-20|happiness:-3",""),
("inspired","İlham Almış",20,"allStats:+20%|plotChance:+100%","Lider güçlenir"),("enraged","Öfkeli",8,"dmg:+30%|armor:-5",""),
("afraid","Korkmuş",6,"flag:flee",""),("drunk","Sarhoş",6,"speed:-20%|happiness:+3",""),
("starving","Açlıktan Ölüyor",0,"hp:-1/tick|happiness:-5","Doygunluk 0 iken"),("exhausted","Bitkin",0,"speed:-30%","Stamina 0"),
("wet","Islak",5,"fireResist:+50%|coldDmg:+30%",""),("irradiated","Işınlanmış",40,"hp:-1/20tick|mutation:+1","Atom sonrası"),
("possessed","Kontrol Ediliyor",0,"flag:player_controlled","Oyuncu bu birimde"),("invisible","Görünmez",10,"flag:untargetable",""),
("charmed","Büyülenmiş",10,"flag:ally_of_caster",""),("rooted","Sarmaşıkla Bağlı",5,"flag:immobile",""),
("haste","Hızlanmış",10,"speed:+50%|atkspd:+30%",""),("weakened","Zayıflamış",10,"dmg:-30%",""),
("mourning","Yasta",30,"happiness:-4","Yakını öldü"),("in_love","Âşık",40,"happiness:+4",""),
("celebrating","Kutlama",10,"happiness:+5","Savaş zaferi, festival"),("turning","Dönüşüyor",2,"hp:-1/20tick|speed:-20%","Süre sonunda zombi; iyileştirilebilir"),("homesick","Sıla Hasreti",20,"happiness:-2","Uzak şehre göç"),
]
save("status_effects",[dict(id="st."+a,name=b,durationMonths=c,effect=d,note=e) for a,b,c,d,e in ST],
     "Statü Efektleri",["id","name","durationMonths","effect","note"])

# ---------------- DISEASES ----------------
DI=[("plague","Kara Veba","contact,rat,cloud",.25,.35,6,"","Kalabalık şehirlerde hızla yayılır"),
("rotbite","Çürük Isırık","bite,cloud",.9,.0,2,"sp.zombie","Isırılan ölünce zombi olur; ölmeden de 2 yılda dönüşür"),
("fleshgrowth","Et Büyümesi","contact",.05,.1,24,"sp.flesh_mound","Tedavi edilmezse et yığınına dönüşür"),
("spores","Mantar Sporu","air",.15,.05,12,"sp.shroomling","Mantar biyomlarında; mantarcığa dönüşür"),
("black_madness","Kara Cinnet","magic",.1,.0,6,"","Herkese saldırır; kutsal ışık iyileştirir"),
("marsh_fever","Bataklık Humması","mosquito",.2,.1,4,"","Yavaşlık ve halsizlik"),
("rabies","Kuduz","bite",.5,.4,3,"","Hayvanlardan geçer; saldırganlık"),
("coughing_sickness","Öksürük Sayrılığı","contact",.3,.03,3,"","Hafif; kışın artar"),]
save("diseases",[dict(id="dis_"+a,name=b,spread=ids(c),contagion=d,lethality=e,durationMonths=f,transformsInto=g or None,note=h)
      for a,b,c,d,e,f,g,h in DI],"Hastalıklar",["id","name","spread","contagion","lethality","note"])

# ---------------- SPELLS ----------------
SP=[("fireball","Ateş Topu",20,60,6,"Alan hasarı 30, tutuşturur"),("fire_breath","Ateş Soluğu",0,40,3,"Koni şeklinde yangın"),
("lightning","Şimşek",25,80,8,"Tek hedef 60, zincirlenir"),("heal","İyileştirme",15,40,4,"Can +40"),
("mass_heal","Toplu Şifa",40,200,4,"Çevredeki müttefikler +30"),("shield","Büyü Kalkanı",15,120,4,"Kalkanlı statüsü"),
("raise_dead","Ölü Diriltme",30,120,5,"Yakındaki cesetlerden iskelet; çağıran medeniyse iskelet onun şehrine katılır"),
("teleport","Işınlanma",20,200,30,"Rastgele ya da ev şehrine"),("curse","Lanet",15,90,6,"Lanetli statüsü"),
("bless","Kutsama",15,90,6,"Kutsanmış statüsü"),("freeze","Dondurma",20,80,6,"Donmuş statüsü, alan"),
("poison_cloud","Zehir Bulutu",25,120,5,"Alan zehirlenmesi"),("summon_imp","İblis Çağırma",40,300,4,"2 kor iblisi"),
("meteor_call","Göktaşı Çağrısı",80,1200,20,"Din büyüsü: hedefe küçük göktaşı"),("quake_stomp","Yer Sarsıntısı",30,150,2,"Çevredekileri sersemletir"),
("entangle","Sarmaşık",15,80,6,"Kök salmış statüsü"),("grow","Büyüme",10,60,6,"Çevrede ağaç/bitki"),
("haste","Hızlandırma",15,90,5,"Hızlanmış statüsü"),("weaken","Zayıflatma",15,90,6,"Zayıflamış statüsü"),
("charm","Büyüleme",30,200,5,"Hedef geçici müttefik"),("invisibility","Görünmezlik",25,240,0,"Kendine"),
("rain_call","Yağmur Duası",40,600,0,"Üstünde yağmur bulutu"),("abduct","Kaçırma Işını",0,60,6,"Birimi gemiye alır"),
("holy_smite","Kutsal Darbe",30,120,6,"Ölümsüzlere 3x hasar"),("rot_breath","Çürük Nefes",20,120,4,"Koni: çürük ısırık bulaştırır"),("cure_rot","Çürük Arındırma",30,240,3,"Dönüşmekte olan birimi iyileştirir"),("acid_spit","Asit Tükürüğü",10,60,4,"Zırhı eritir: zırh -5, 10 hasar/sn"),("inspire","İlham Verme",30,300,6,"Müttefiklere ilham"),
]
save("spells",[dict(id="spell."+a,name=b,manaCost=c,cooldownTicks=d,range=e,effect=f) for a,b,c,d,e,f in SP],
     "Büyüler",["id","name","manaCost","cooldownTicks","range","effect"])

# ---------------- EQUIPMENT ----------------
W=[("sword","Kılıç","weapon","dmg:+8|atkspd:+0",1),("axe","Balta","weapon","dmg:+11|atkspd:-10%",1),("spear","Mızrak","weapon","dmg:+7|range:+1",1),
("bow","Yay","weapon","dmg:+5|range:+6",1),("crossbow","Arbalet","weapon","dmg:+9|range:+5|atkspd:-25%",1),("hammer","Savaş Çekici","weapon","dmg:+13|knockback:+50%|atkspd:-20%",1),
("dagger","Hançer","weapon","dmg:+4|atkspd:+30%|crit:+10",1),("staff","Asa","weapon","mana:+30|spellPower:+20%",1),("club","Sopa","weapon","dmg:+4",1),
("trident","Üç Dişli Zıpkın","weapon","dmg:+7|range:+1|waterDmg:+30%",1),("sling","Sapan","weapon","dmg:+3|range:+4",1),
("helmet","Miğfer","helmet","armor:+3",1),("armor","Zırh","armor","armor:+8|speed:-5%",1),("shield","Kalkan","shield","armor:+4|block:+15",1),
("boots","Çizme","boots","speed:+5%",1),("ring","Yüzük","ring","luck:+5",0),("amulet","Muska","amulet","mana:+10|diseaseResist:+10%",0)]
save("equipment_types",[dict(id="eq."+a,name=b,slot=c,baseEffects=mods(d),usesMaterial=bool(e)) for a,b,c,d,e in W],
     "Ekipman Tipleri",["id","name","slot","baseEffects"])
M=[("wood","Ahşap",.6,0,""),("bone","Kemik",.7,0,""),("copper","Bakır",.9,1,""),("bronze","Tunç",1.0,2,"Bakır+kalay"),
("iron","Demir",1.2,3,""),("steel","Çelik",1.45,4,"Demir+kömür"),("silver","Gümüş",1.2,4,"Ölümsüzlere +%50 hasar"),
("obsidian","Obsidyen",1.5,5,"Kritik +%10, kırılgan"),("skyiron","Gökdemir",1.8,6,"Hafif: hız cezası yok"),
("crystal","Kristal",1.4,6,"Büyü gücü +%30"),("starore","Yıldız Çeliği",2.3,8,"Efsanevi silahların malzemesi")]
save("materials",[dict(id="mat."+a,name=b,multiplier=c,tier=d,special=e) for a,b,c,d,e in M],"Malzemeler",["id","name","multiplier","tier","special"])
Q=[("crude","Kaba",.7,.30),("common","Sıradan",1.0,.45),("fine","İyi",1.2,.15),("masterwork","Usta İşi",1.45,.07),("unique","Eşsiz",1.8,.025),("legendary","Efsanevi",2.5,.005)]
save("item_qualities",[dict(id="q."+a,name=b,multiplier=c,baseChance=d) for a,b,c,d in Q],"Eşya Kalite Seviyeleri",["id","name","multiplier","baseChance"])

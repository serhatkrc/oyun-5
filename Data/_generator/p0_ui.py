from lib import *

# ---------------- UI STRINGS (localization keys; tr = source, en = translation) ----------------
S = [
("ui.pause","Duraklat","Pause"),("ui.play","Oynat","Play"),("ui.step","Tek Adım","Step"),
("ui.super_speed","Süper Hız","Super Speed"),("ui.speed_fmt","×{0}","×{0}"),
("ui.date_fmt","Yıl {0} · Ay {1}","Year {0} · Month {1}"),
("ui.tab.world","Dünya","World"),("ui.tab.civ","Medeniyetler","Civilizations"),("ui.tab.creatures","Yaratıklar","Creatures"),
("ui.tab.nature","Doğa ve Afetler","Nature & Disasters"),("ui.tab.destruction","Yıkım","Destruction"),("ui.tab.other","Diğer","Other"),
("ui.brush_size","Fırça","Brush"),("ui.shape.circle","Daire","Circle"),("ui.shape.square","Kare","Square"),
("ui.menu","Menü","Menu"),("ui.new_world","Yeni Dünya","New World"),("ui.generate","Üret","Generate"),("ui.cancel","Vazgeç","Cancel"),
("ui.close","Kapat","Close"),("ui.template","Şablon","Template"),("ui.size","Boyut","Size"),("ui.seed","Tohum","Seed"),
("ui.randomize","Rastgele","Randomize"),("ui.image_path","Görüntü yolu (PNG)","Image path (PNG)"),
("ui.land_ratio","Kara oranı","Land ratio"),("ui.mountain_ratio","Dağ oranı","Mountain ratio"),("ui.hill_ratio","Tepe oranı","Hill ratio"),
("ui.roughness","Kıyı girintisi","Roughness"),("ui.forest_density","Orman yoğunluğu","Forest density"),
("ui.biome_variety","Biyom çeşitliliği","Biome variety"),("ui.ore_density","Maden yoğunluğu","Ore density"),
("ui.temperature","Sıcaklık","Temperature"),("ui.moisture","Nem","Moisture"),
("ui.size.tiny","Çok Küçük","Tiny"),("ui.size.small","Küçük","Small"),("ui.size.medium","Orta","Medium"),("ui.size.large","Büyük","Large"),
("ui.size.huge","Çok Büyük","Huge"),("ui.size.giant","Devasa","Giant"),("ui.size.titanic","Titanik","Titanic"),
("ui.save","Kaydet","Save"),("ui.load","Yükle","Load"),("ui.delete","Sil","Delete"),("ui.save_load","Kayıtlar","Saves"),
("ui.slot_fmt","Slot {0}","Slot {0}"),("ui.autosave_fmt","Otomatik {0}","Autosave {0}"),("ui.empty_slot","Boş","Empty"),
("ui.slot_info_fmt","{0} · Yıl {1} · {2}×{3}","{0} · Year {1} · {2}×{3}"),
("ui.saved","Dünya kaydedildi","World saved"),("ui.loaded","Dünya yüklendi","World loaded"),
("ui.save_failed","Kayıt başarısız: {0}","Save failed: {0}"),("ui.load_failed","Yükleme başarısız: {0}","Load failed: {0}"),
("ui.confirm_delete","Silmek için tekrar bas","Press again to delete"),
("ui.content_errors","İçerik verisinde hata var; oyun başlatılamadı:","Content data has errors; the game cannot start:"),
("ui.load_warnings","Kayıttaki bazı içerikler bulunamadı:","Some saved content was not found:"),
("ui.era_fmt","{0} · {1} yıl","{0} · {1} yrs"),("ui.era_frozen_fmt","{0} · sabit","{0} · frozen"),
("ui.era_changed_fmt","Yeni çağ: {0}","A new era begins: {0}"),("ui.disaster_fmt","Afet: {0}","Disaster: {0}"),
]
save("ui_strings",[dict(id=a,tr=b,en=c) for a,b,c in S],"Arayüz Metinleri",["id","tr","en"])

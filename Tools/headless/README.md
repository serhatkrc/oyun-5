# Headless harness (Unity olmadan derleme ve simülasyon)

Unity kurulu olmayan ortamlarda (ör. bulut oturumları) kodu kontrol etmek için. **Unity her zaman asıl doğrulamadır**; bu araç yalnızca erken uyarı verir.

```
python3 Tools/headless/headless.py check              # tüm runtime asmdef'leri tip kontrolünden geçirir
python3 Tools/headless/headless.py run determinism 1000
python3 Tools/headless/headless.py run units          # NatureSoak ekosistem + birim performansı
python3 Tools/headless/headless.py run soak           # NatureSoak tamamı
python3 Tools/headless/headless.py run tests          # Tests/Headless altındaki kontroller
```

- `check`: UnityEngine 2021.3 referans assembly'leri (NuGet `UnityEngine.Modules`) + `stubs/` altındaki imza taklitleri (Burst, Collections paketi, Input System). Unity 6'ya özgü bir API taklitte yoksa yanlış hata verebilir; o zaman `stubs/CompileOnlyPackages.cs`'e ekleyin. Editor ve Test assembly'leri derlenmez.
- `run`: Core, Content, World, WorldGen, Sim, Powers, Persistence katmanlarını `shim/` altındaki yönetilen (managed) Unity taklitleriyle .NET 8'de çalıştırır. Job'lar tek iş parçacığında sırayla koşar, Burst yoktur: süreler yalnızca önce/sonra karşılaştırması içindir.
- `gen_math.py`: Unity.Mathematics'in kullanılan alt kümesinin çalışan bir kopyasını üretir (`noise.snoise` Unity'ninkiyle bit düzeyinde aynı değildir; üretilen dünyalar Unity'dekinden farklı olabilir).
- Gereken: .NET 8 SDK, ilk çalıştırmada api.nuget.org erişimi. Çıktılar `Tools/headless/.build/` (git dışı).

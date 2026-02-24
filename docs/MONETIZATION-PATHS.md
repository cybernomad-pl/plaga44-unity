# Sciezki monetyzacji -- PLAGA '44 VR

Stan: 2026-02-24
Kontekst: Mamy dzialajace mechaniki VR -- grabbing, throwing z gaze-correction, hit detection, body part detachment, physics stacking. Kazdy kierunek ponizej pozwala (a) dopracowac mechaniki, (b) wypuscic cos do sprzedazy na Meta Store lub Steam.

---

## 1. Rzutki (VR Darts)
- Klasyczna gra w rzutki, tarcza na scianie
- Mechanika: precyzyjne rzucanie z gaze-correction (juz mamy)
- Maly scope, szybki do wypuszczenia
- Multiplayer: turowy, latwy do dodania
- Platforma: Meta Quest (standalone), Steam VR

## 2. Gra w czolgi -- piaskownica
- Piaskownica z modelami z piasku (czolgi, statki, zamki)
- Gracz niszczy je kamieniami -- physics destruction
- Mechanika: rzucanie + hit detection + body part detachment (juz mamy!)
- Rozne poziomy trudnosci (odleglosc, rozmiar celu, wiatr?)
- Tryb kreatywny: buduj i niszcz

## 3. Symulator protestow
- Kamienie, butelki, koktajle Molotowa, cegly
- Rzucanie w cele / barykady / wozy policyjne
- Kontrowersyjny ale viralowy potencjal
- Physics destruction + fire effects
- Rozne scenariusze/lokacje

## 4. Skeeball VR
- Klasyczny skeeball z wesołego miasteczka
- Toczenie/rzucanie kulek w rampy z punktami
- Prosty, uzalezniajacy gameplay loop
- Leaderboardy, daily challenges
- Arcade vibe, niski prog wejscia

## 5. Rzut workiem (Cornhole VR)
- Popularna gra imprezowa (USA, ale rosnie globalnie)
- Rzut workiem z grochem w dziure w desce
- Multiplayer naturalny (2v2)
- Turniejowy format
- Physics: worek vs twarda deska, slizg, wpadanie

## 6. Rozbijanie puszek -- wesole miasteczko
- Stoisko z piramidka puszek
- Rozne itemy do rzucania (pilki, kamienie)
- Power-upy, specjalne puszki (eksplodujace, zamrozone)
- Progression: odblokowywanie stoisk/levelow
- Arcade, rodzinny, szeroka grupa docelowa

## 7. [DO UZUPELNIENIA]
- ...kolejne pomysly dopisywac tutaj

---

## Wspolny rdzen techniczny
Wszystkie powyzsze korzystaja z tego samego stacku:
- VR grab/throw (OVRGrabber + GazeThrow)
- Physics collision + destruction (HitDetector + HitZone + Rigidbody)
- Body part detachment (HitTarget + HitZone.Detach/Explode)
- Compound colliders (cross-shaped BoxCollider stacking)
- Gaze-corrected throwing (3 strefy, blending reka/wzrok)

Kazdy nowy kierunek = nowy content + tuning istniejacych mechanik.
Prototyp nowej gry = kilka dni pracy, nie miesiecy.

---

## Strategia
- Wypuscic NAJPROSTSZA gre pierwszy (rzutki? puszki? skeeball?)
- Zebrać feedback na mechanike rzucania
- Iterowac na kolejnych tytulach
- PLAGA '44 (survival) jako long-term -- grant IPK 400K PLN
- Mini-gry jako cashflow + dopracowanie core mechanik

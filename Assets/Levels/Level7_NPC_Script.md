# Level 7 — NPC-manus

## Scen
Bergstopp. Räddningsuppdrag. Två NPC:er väntar på helikoptern.

---

## Karaktärer

### Nils (röd jacka) — den skadade
- Ligger på marken med brutet ben
- Växlar slumpmässigt mellan liggande animationer (t.ex. vrider sig, lyfter armen)
- Rör sig aldrig — stannar kvar på marken hela scenen

**Animationer att koppla:**
- [ ] Liggande idle 1
- [ ] Liggande idle 2 (valfri extra variant)

---

### Sven (blå jacka) — den oskadade vännen
- Står ca 3 meter från Nils
- Spelar vinkningsanimation i loop
- Roterar kontinuerligt så han alltid är vänd mot helikoptern (Y-axeln)
- När helikoptern landat tillräckligt nära: slutar vinka och går mot helikoptern
- Triggers fade-out när han nått helikoptern (eller efter X sekunder)
- *Vad händer sen visas inte — spelaren får föreställa sig att pilot och Sven tillsammans bär in Nils*

**Animationer att koppla:**
- [ ] Vinka i loop
- [ ] Gå mot helikoptern

---

## Flöde

```
GameController aktiverar NPC:erna
        │
        ▼
Sven: börjar vinka + vänder sig mot helikoptern (varje frame)
Nils: växlar slumpmässigt mellan liggande animationer
        │
        ▼
[Villkor] Helikoptern landar inom X meter från Nils/Sven
        │
        ▼
Sven: slutar vinka, går mot helikoptern
Nils: ligger kvar (brutet ben)
        │
        ▼
Fade-out → uppdrag klart
(pilot + Sven bär in Nils — visas ej)
```

---

## Animator-parametrar

### Nils
| Parameter     | Typ | Syfte                                        |
|---------------|-----|----------------------------------------------|
| `lyingVariant`| Int | Vilken liggande animation som spelas (0, 1, …) |

### Sven
| Parameter     | Typ     | Syfte                            |
|---------------|---------|----------------------------------|
| `startWalking`| Trigger | Aktiveras när helikoptern landat |

---

## GameController-ansvar
- Aktivera Nils och Sven när Level 7 startar
- Varje frame: rotera Sven mot helikopterns position (Y-axeln)
- Lyssna på landningsvillkor (avstånd + hastighet)
- Trigga `startWalking` på Sven när villkoret uppfylls
- Starta fade-out när Sven nått helikoptern (eller efter X sekunder)

---

## Landningsvillkor (förslag)
- Avstånd helikopter ↔ Nils: **< 15 meter**
- Vertikal hastighet: **< 1 m/s**
- AGL: **< 2 meter**

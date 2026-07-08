# Fysikplan — translational lift, torque, väderflöjel, tyngdpunkt

Mål: gejmig helikopterkänsla ("hjälp, den försöker ha ihjäl mig"), inte perfekt simulering.
Allt byggs som enkla krafter/moment ovanpå befintliga `ComputeForce`/`ComputeTorque` i
`Assets/Scripts/SimCore/Simulator.cs`. Inga nya system, ingen extern fysik.

Alla effekter får egna gain-parametrar i `SimulatorConfig` så att de kan trimmas
(eller nollas) oberoende av varandra.

---

## 1. Translational lift (ETL)

**Känsla:** när farten ökar "bär" rotorn bättre — helikoptern lyfter vid ~15–25 kt
utan mer collective, och kan cruisa ganska plant med mindre effekt.

**Modell:** lyftkraften multipliceras med en fartbonus baserad på horisontell fart:

```
hSpeed   = |velocity.xz|
etl      = smoothstep(etlStartSpeed, etlFullSpeed, hSpeed)      // 0 → 1
lift    *= 1 + etlLiftBonus * etl
```

Nya config-fält:

| Fält | Förslag | Betydelse |
|---|---|---|
| `etlStartSpeed` | 4 m/s (~8 kt) | bonus börjar här |
| `etlFullSpeed` | 13 m/s (~25 kt) | bonus maxad här |
| `etlLiftBonus` | 0.25 | +25 % lyft vid full ETL |

**Delmoment: flapback (rekommenderas, hör ihop med "rätar upp sig").**
Vid fart framåt får helikoptern ett svagt nos-upp-moment ∝ fart längs kroppens
framåtaxel. Det gör att den vill plana ut av sig själv, att man måste hålla lite
framåt-cyclic i cruise (precis som på riktigt), och det sätter en naturlig toppfart.

```
pitchUp = flapbackGain * forwardSpeed        // kring pitch-axeln
```

`flapbackGain` ≈ 0.4 deg/s² per m/s → ~12 deg/s² vid 30 m/s, hålls emot med ~0.2 cyclic.

---

## 2. Rotor-torque (anti-torque med pedalerna)

**Känsla:** drar man på collective vill nosen gira — pilot måste ge motsatt pedal.

**Riktning (MD500):** huvudrotorn går **motsols sedd uppifrån** → reaktionsmomentet på
skrovet blir medsols uppifrån → **nosen girar åt höger** → piloten kompenserar med
**vänster pedal**. I Unity (vänsterhänt, Y upp) är det **positivt** moment kring
kroppens upp-axel.

**Modell (gejmig kompromiss):** momentet trimmas vid hover-collective (0.5), så att man
inte behöver stå på pedalen konstant — men varje collective-ändring kräver kompensation:

```
yawReaction = rotorTorqueGain * (collective - 0.5) * 2   // kring kroppens upp-axel, positivt = nos höger
```

`rotorTorqueGain` ≈ 50 deg/s² → ±25 deg/s² vid fullt utslag från hover. Att jämföra med
`pedalTorque` = 80, så det går att hålla emot men känns.

(Full realism = moment ∝ absolut collective utan trim. Kan göras valbart senare med en
config-flagga, men trimmad variant föreslås som standard.)

---

## 3. Väderflöjel + fartberoende pedalauktoritet

**Känsla:** i fart vill luftströmmen hålla nosen i färdriktningen. Pedalerna blir tröga —
man svänger genom att banka och finjusterar med pedalerna.

**Modell, två delar i `ComputeTorque`:**

**a) Väderflöjelmoment** — girmoment som vrider nosen mot fartvektorn:

```
flatFwd  = kroppens framåtaxel projicerad på horisontalplanet
flatVel  = velocity.xz
error    = SignedAngle(flatFwd, flatVel, up)             // grader
yawVane  = weathervaneGain * hSpeed * error              // clampat, fade-in över ~3 m/s
```

`weathervaneGain` ≈ 0.02 deg/s² per (m/s·deg) → vid 20 m/s och 30° sidglidning ≈ 12 deg/s².

**b) Pedalauktoritet skalas ned med fart:**

```
authority   = Lerp(1.0, pedalAuthorityHighSpeed, InverseLerp(minSpeed, maxSpeed, hSpeed))
pedalTorque *= authority
```

| Fält | Förslag |
|---|---|
| `pedalAuthorityMinSpeed` | 6 m/s (full auktoritet under) |
| `pedalAuthorityMaxSpeed` | 20 m/s |
| `pedalAuthorityHighSpeed` | 0.3 (30 % kvar i cruise) |

**Sväng genom bankning kommer gratis:** lyftkraften följer redan kroppens upp-axel, så
bankning ger en horisontell kraftkomposant som kröker banan; väderflöjeln vrider sedan
nosen med i svängen → koordinerade svängar uppstår utan extra kod.

---

## 4. Tyngdpunkt strax under rotornavet

**Känsla:** pedalgir sker runt masten, inte runt någon godtycklig kollider-mittpunkt.

**Modell:** sätt `rb.centerOfMass` explicit i `SimulatorDriver.Awake()`.

- Nytt fält på SimulatorDriver: `public Transform centerOfMassMarker` — ett tomt GO som
  placeras strax under huvudrotornavet i editorn (samma mönster som SpawnPoint/seat-markörer).
- `rb.centerOfMass = rb.transform.InverseTransformPoint(centerOfMassMarker.position);`
- Fallback: om markören saknas, rör inte COM (dagens beteende).
- ContextMenu-hjälpare "Log COM Info" som loggar auto-beräknad vs satt COM för kalibrering.

Alla `AddTorque`-moment verkar automatiskt runt COM, så pedalgir, väderflöjel och
rotor-torque roterar runt masten utan ytterligare kod.

**Samtidig städning:** `ComputeTorque` använder idag `Vector3.up` som gir-axel (världen).
Byts till kroppens upp-axel så att gir i bankat läge sker runt rotoraxeln — krävs ändå
för att punkt 2 och 3 ska kännas rätt.

---

## Filer som ändras

| Fil | Ändring |
|---|---|
| `Simulator.cs` | Nya config-fält; ETL + flapback i `ComputeForce`; rotor-torque, väderflöjel, pedalauktoritet i `ComputeTorque`. `ComputeTorque` får ny parameter `Vector3 velocity` (behövs för 1b/3). |
| `SimulatorDriver.cs` | Skicka `rb.velocity` till `ComputeTorque`; COM-markör + sättning i Awake; ContextMenu-hjälpare. |
| Scen | Nytt tomt GO "CenterOfMass" under rotornavet, kopplas i Inspektorn. |

Implementationsordning: **4 → 2 → 1 → 3** (COM först eftersom den ändrar baskänslan i
all rotation, sedan effekterna i stigande komplexitet, med speltest mellan varje).

## Verifiering (speltest per steg)

1. **COM:** hover, ge pedal — helikoptern ska snurra runt masten, inte "svepa" med svansen.
2. **Torque:** hover, ryck upp collective — nosen ska gira höger; vänster pedal håller emot.
3. **ETL:** accelerera framåt med konstant collective — vid ~15–20 kt ska den börja stiga;
   flapback ska ge mjuk nos-upp-tendens som kräver framåt-cyclic i cruise.
4. **Väderflöjel:** i 20 m/s cruise, ge full pedal — nosen ska ge sig av trögt och söka
   tillbaka mot färdriktningen när pedalen släpps. Banka åt sidan — helikoptern ska carva
   en sväng med nosen följande banan.
5. **Regression:** Level 1–3 (hover-träning) ska kännas som förut — alla nya effekter är
   ~noll vid hover (ETL/väderflöjel kräver fart, torquen är trimmad vid hover-collective).

## Öppna frågor

1. **Torque-trim:** OK att trimma reaktionsmomentet vid hover-collective (förslaget), eller
   vill du ha konstant moment som kräver ständig pedal (mer realistiskt, mer tröttande)?
2. **Flapback:** OK att ta med nos-upp-vid-fart som del av punkt 1? Det är den mekanism som
   ger "rätar upp sig"-känslan — utan den behåller helikoptern exakt den pitch man ger den.
3. **Sidoglidnings-drag:** vill du dessutom att sidledes flygning ska bromsa mer än framåt
   (flat platta i sidled)? Förstärker väderflöjelkänslan men är inte nödvändigt för punkterna.

"""Génère les visuels de la société de démonstration (logo, cachet, signature)
et les justificatifs scannés utilisés par le générateur d'historique.

Usage : python generer_visuels.py
Sortie : le dossier Assets/ à côté de ce script.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ICI = Path(__file__).resolve().parent
SORTIE = ICI / "Assets"
JUSTIFS = SORTIE / "justificatifs"
FONTS = Path("C:/Windows/Fonts")

SOCIETE = "NÉOFLUX SOLUTIONS"
SOCIETE_COURT = "Néoflux Solutions SARL"
MF = "1587342/T/A/M/000"
VILLE = "TUNIS"

NAVY = (22, 43, 84)
TEAL = (0, 150, 160)
INK_BLUE = (28, 52, 140)


def font(nom: str, taille: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / nom), taille)


def logo() -> None:
    w, h = 900, 260
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    # Emblème : deux flux qui se croisent dans un hexagone
    cx, cy, r = 130, 130, 105
    pts = [(cx + r * math.cos(math.radians(60 * i - 30)), cy + r * math.sin(math.radians(60 * i - 30))) for i in range(6)]
    d.polygon(pts, fill=NAVY)
    for k, couleur in enumerate([TEAL, (255, 255, 255)]):
        off = -28 + 56 * k
        d.line([(cx - 62, cy + off + 30), (cx - 10, cy + off - 10), (cx + 20, cy + off + 12), (cx + 64, cy + off - 30)],
               fill=couleur, width=18, joint="curve")
    d.text((265, 52), "Néoflux", font=font("segoeuib.ttf", 104), fill=NAVY)
    d.text((272, 178), "SOLUTIONS  ·  ERP & SERVICES IT", font=font("segoeui.ttf", 34), fill=TEAL)
    img.save(SORTIE / "logo.png")


def cachet() -> None:
    taille = 600
    img = Image.new("RGBA", (taille, taille), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    c = taille // 2
    d.ellipse([20, 20, taille - 20, taille - 20], outline=INK_BLUE + (235,), width=12)
    d.ellipse([70, 70, taille - 70, taille - 70], outline=INK_BLUE + (235,), width=5)
    # Texte circulaire
    f = font("arialbd.ttf", 44)
    texte = f"{SOCIETE} • {VILLE} • "
    rayon = 232
    n = len(texte)
    for i, ch in enumerate(texte):
        angle = -90 + 360 * i / n
        lettre = Image.new("RGBA", (70, 70), (0, 0, 0, 0))
        ImageDraw.Draw(lettre).text((35, 35), ch, font=f, fill=INK_BLUE + (240,), anchor="mm")
        lettre = lettre.rotate(-angle - 90, resample=Image.BICUBIC)
        x = c + rayon * math.cos(math.radians(angle)) - 35
        y = c + rayon * math.sin(math.radians(angle)) - 35
        img.alpha_composite(lettre, (int(x), int(y)))
    d.text((c, c - 52), "SARL", font=font("arialbd.ttf", 58), fill=INK_BLUE + (240,), anchor="mm")
    d.text((c, c + 10), f"MF {MF}", font=font("arialbd.ttf", 30), fill=INK_BLUE + (240,), anchor="mm")
    d.text((c, c + 60), "Les Berges du Lac", font=font("arial.ttf", 30), fill=INK_BLUE + (240,), anchor="mm")
    # Usure d'encre réaliste
    rng = random.Random(7)
    px = img.load()
    for _ in range(9000):
        x, y = rng.randrange(taille), rng.randrange(taille)
        r_, g_, b_, a_ = px[x, y]
        if a_ > 0:
            px[x, y] = (r_, g_, b_, int(a_ * rng.uniform(0.25, 0.8)))
    img = img.rotate(-11, resample=Image.BICUBIC)
    img.save(SORTIE / "cachet.png")


def signature() -> None:
    w, h = 700, 260
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.text((40, 30), "L. Ben Salem", font=font("segoescb.ttf", 92), fill=INK_BLUE + (245,))
    # Paraphe
    pts = []
    for t in range(0, 360, 4):
        x = 90 + t * 1.55
        y = 205 + 18 * math.sin(math.radians(t * 1.8)) - t * 0.06
        pts.append((x, y))
    d.line(pts, fill=INK_BLUE + (230,), width=5, joint="curve")
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.save(SORTIE / "signature.png")


# ---------------------------------------------------------------- justificatifs
FOURNISSEURS_SCAN = [
    # nom, mf, adresse, type, lignes (designation, qte, pu, tva)
    ("InfoDistrib Tunisie SARL", "1293847/K/A/M/000", "Zone industrielle Charguia II, Tunis", "facture",
     [("Disque SSD 1 To Samsung", 4, 365.0, 19), ("Barrette RAM 16 Go DDR4", 6, 189.0, 19)]),
    ("Bureau Plus Fournitures", "1102938/B/A/M/000", "45 Avenue de Carthage, Tunis", "facture",
     [("Ramettes papier A4 (carton de 5)", 6, 62.5, 19), ("Toner laser HP 26A", 2, 245.0, 19)]),
    ("Cloud Maghreb Hosting", "1456021/H/A/M/000", "Technopôle El Ghazala, Ariana", "facture",
     [("Hébergement VPS 8 vCPU - mensuel", 1, 420.0, 19), ("Sauvegarde externalisée 500 Go", 1, 95.0, 19)]),
    ("Station Lac Carburants", "0981234/S/N/M/000", "Route de La Marsa, Tunis", "ticket",
     [("Gasoil sans soufre (litres)", 48, 2.525, 19)]),
    ("Restaurant Le Golfe", "1311208/R/N/M/000", "Rue du Lac Léman, Les Berges du Lac", "ticket",
     [("Déjeuner client (4 couverts)", 1, 186.0, 7)]),
    ("Imprimerie Al Wafa", "1210457/I/A/M/000", "12 Rue de Marseille, Tunis", "facture",
     [("Cartes de visite (lot de 500)", 3, 58.0, 19), ("Plaquettes commerciales A4", 200, 1.35, 19)]),
    ("Clim Services Plus", "1388765/C/A/M/000", "Avenue Habib Bourguiba, Le Kram", "facture",
     [("Maintenance climatisation salle serveurs", 1, 380.0, 19)]),
    ("TechnoNet Câblage", "1477230/T/A/M/000", "Rue Ibn Khaldoun, Ben Arous", "facture",
     [("Câble réseau Cat6 (bobine 305 m)", 2, 310.0, 19), ("Connecteurs RJ45 (boîte de 100)", 3, 42.0, 19)]),
    ("Librairie Al Kitab", "0874512/L/N/M/000", "Avenue de France, Tunis", "ticket",
     [("Code de la TVA - édition 2026", 2, 38.0, 7), ("Agenda professionnel 2027", 4, 24.5, 19)]),
    ("Taxi Express Tunis", "0765390/T/N/P/000", "Tunis", "ticket",
     [("Course aéroport Tunis-Carthage", 1, 28.0, 19)]),
]


def justificatif(i: int, data, date_txt: str, numero: str) -> Path:
    nom, mf, adresse, kind, lignes = data
    if kind == "ticket":
        w, h = 620, 900
    else:
        w, h = 1000, 1300
    img = Image.new("RGB", (w, h), (255, 255, 255))
    d = ImageDraw.Draw(img)
    big = font("arialbd.ttf", 40 if kind != "ticket" else 34)
    mid = font("arial.ttf", 26 if kind != "ticket" else 24)
    bold = font("arialbd.ttf", 26 if kind != "ticket" else 24)
    y = 40
    d.text((40, y), nom.upper(), font=big, fill=(20, 20, 20)); y += 58
    d.text((40, y), adresse, font=mid, fill=(60, 60, 60)); y += 36
    d.text((40, y), f"Matricule fiscal : {mf}", font=mid, fill=(60, 60, 60)); y += 60
    titre = "FACTURE" if kind == "facture" else "TICKET DE CAISSE"
    d.text((40, y), f"{titre} N° {numero}", font=bold, fill=(10, 10, 10)); y += 38
    d.text((40, y), f"Date : {date_txt}", font=mid, fill=(10, 10, 10)); y += 38
    d.text((40, y), f"Client : {SOCIETE_COURT}", font=mid, fill=(10, 10, 10)); y += 38
    d.text((40, y), "MF client : 1587342/T/A/M/000", font=mid, fill=(10, 10, 10)); y += 56
    d.line([(40, y), (w - 40, y)], fill=(0, 0, 0), width=2); y += 16
    ht = 0.0
    tva = 0.0
    for des, q, pu, t in lignes:
        mt = round(q * pu, 3)
        ht += mt
        tva += round(mt * t / 100, 3)
        d.text((40, y), des[:42], font=mid, fill=(10, 10, 10))
        d.text((w - 40, y), f"{mt:,.3f}".replace(",", " "), font=mid, fill=(10, 10, 10), anchor="ra")
        y += 34
        d.text((60, y), f"{q} x {pu:,.3f}  TVA {t}%".replace(",", " "), font=mid, fill=(90, 90, 90))
        y += 42
    d.line([(40, y), (w - 40, y)], fill=(0, 0, 0), width=2); y += 20
    timbre = 1.0 if kind == "facture" else 0.0
    ttc = round(ht + tva + timbre, 3)
    for lab, val, f_ in [("Total HT", ht, mid), ("TVA", tva, mid)] + ([("Timbre fiscal", timbre, mid)] if timbre else []) + [("TOTAL TTC", ttc, bold)]:
        d.text((w // 2 - 60, y), lab, font=f_, fill=(10, 10, 10))
        d.text((w - 40, y), f"{val:,.3f} TND".replace(",", " "), font=f_, fill=(10, 10, 10), anchor="ra")
        y += 40
    y += 30
    d.text((40, y), "Réglé par carte bancaire" if kind == "ticket" else "Règlement : virement à 30 jours", font=mid, fill=(60, 60, 60))
    d.text((40, h - 70), "Merci de votre confiance.", font=mid, fill=(90, 90, 90))
    # Effet "photo de téléphone" léger : rotation, flou et grain
    rng = random.Random(i)
    img = img.rotate(rng.uniform(-2.2, 2.2), resample=Image.BICUBIC, expand=True, fillcolor=(238, 236, 230))
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    chemin = JUSTIFS / f"justificatif_{i:02d}.jpg"
    img.convert("RGB").save(chemin, quality=88)
    return chemin


def main() -> None:
    SORTIE.mkdir(parents=True, exist_ok=True)
    JUSTIFS.mkdir(parents=True, exist_ok=True)
    logo()
    cachet()
    signature()
    # Les dates et numéros sont repris tels quels par le générateur C# (Assets/justificatifs.csv).
    dates = ["2026-06-04", "2026-06-23", "2026-07-08", "2026-07-21", "2026-08-12",
             "2026-09-03", "2026-09-25", "2026-10-09", "2026-10-28", "2026-11-12"]
    lignes_csv = ["fichier;fournisseur;matricule;type;date;numero;total_ht;total_tva;total_ttc"]
    for i, (data, dt) in enumerate(zip(FOURNISSEURS_SCAN, dates), start=1):
        numero = f"{'F' if data[3] == 'facture' else 'T'}{dt[2:4]}{dt[5:7]}-{100 + i * 7:04d}"
        j, m, a = dt[8:10], dt[5:7], dt[0:4]
        chemin = justificatif(i, data, f"{j}/{m}/{a}", numero)
        ht = sum(round(q * pu, 3) for _, q, pu, _ in data[4])
        tva = sum(round(round(q * pu, 3) * t / 100, 3) for _, q, pu, t in data[4])
        timbre = 1.0 if data[3] == "facture" else 0.0
        lignes_csv.append(f"{chemin.name};{data[0]};{data[1]};{data[3]};{dt};{numero};{ht:.3f};{tva:.3f};{ht + tva + timbre:.3f}")
    (SORTIE / "justificatifs.csv").write_text("\n".join(lignes_csv), encoding="utf-8")
    print("Visuels générés dans", SORTIE)


if __name__ == "__main__":
    main()

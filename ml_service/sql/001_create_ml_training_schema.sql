Exit code: 0
Wall time: 0.5 seconds
Output:
-- Jeu de donnees d'entrainement isole pour la prediction de retard.
-- Ce script ne modifie aucune table du schema public de l'application.

CREATE SCHEMA IF NOT EXISTS ml_training;

CREATE TABLE IF NOT EXISTS ml_training.generation_runs (
    id BIGSERIAL PRIMARY KEY,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    clients_count INTEGER NOT NULL,
    invoices_count INTEGER NOT NULL,
    notes TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ml_training.clients (
    client_key INTEGER PRIMARY KEY,
    nom_fictif TEXT NOT NULL,
    type_client TEXT NOT NULL CHECK (type_client IN ('B2B', 'B2C')),
    ville TEXT NOT NULL,
    anciennete_mois INTEGER NOT NULL CHECK (anciennete_mois >= 1),
    risque_comportement NUMERIC(4,3) NOT NULL CHECK (risque_comportement BETWEEN 0 AND 1)
);

CREATE TABLE IF NOT EXISTS ml_training.factures (
    facture_key BIGINT PRIMARY KEY,
    client_key INTEGER NOT NULL REFERENCES ml_training.clients(client_key),
    date_emission DATE NOT NULL,
    date_echeance DATE NOT NULL,
    date_paiement DATE NULL,
    montant_ttc NUMERIC(15,3) NOT NULL CHECK (montant_ttc > 0),
    mode_paiement TEXT NOT NULL,
    statut TEXT NOT NULL,
    CONSTRAINT ck_factures_dates CHECK (date_echeance >= date_emission)
);

CREATE TABLE IF NOT EXISTS ml_training.paiements (
    paiement_key BIGINT PRIMARY KEY,
    facture_key BIGINT NOT NULL UNIQUE REFERENCES ml_training.factures(facture_key),
    montant NUMERIC(15,3) NOT NULL CHECK (montant > 0),
    date_paiement DATE NOT NULL,
    banque TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_ml_factures_client_date
    ON ml_training.factures(client_key, date_emission, facture_key);

CREATE INDEX IF NOT EXISTS ix_ml_factures_echeance
    ON ml_training.factures(date_echeance);

-- Regeneration reproductible : seules les donnees d'entrainement sont remplacees.
TRUNCATE TABLE ml_training.paiements, ml_training.factures, ml_training.clients;

INSERT INTO ml_training.clients (
    client_key, nom_fictif, type_client, ville, anciennete_mois, risque_comportement
)
SELECT
    i,
    'Client fictif ' || LPAD(i::TEXT, 4, '0'),
    CASE WHEN i % 5 = 0 THEN 'B2C' ELSE 'B2B' END,
    (ARRAY['Tunis', 'Sfax', 'Sousse', 'Ariana', 'Nabeul', 'Monastir'])[(i % 6) + 1],
    6 + ((i * 11) % 115),
    ROUND((0.050 + ((i * 17) % 46) / 100.0)::NUMERIC, 3)
FROM generate_series(1, 500) AS i;

WITH base AS (
    SELECT
        s::BIGINT AS facture_key,
        (1 + ((s * 37) % 500))::INTEGER AS client_key,
        (DATE '2024-01-01' + ((s * 13) % 820)) AS date_emission,
        CASE
            WHEN s % 5 = 0 THEN 60
            WHEN s % 3 = 0 THEN 45
            ELSE 30
        END AS delai_contractuel,
        ROUND((200 + ((s * 791) % 240000) / 10.0)::NUMERIC, 3) AS montant_ttc,
        CASE s % 4
            WHEN 0 THEN 'Virement'
            WHEN 1 THEN 'Cheque'
            WHEN 2 THEN 'Especes'
            ELSE 'Carte'
        END AS mode_paiement,
        c.risque_comportement
    FROM generate_series(1, 10000) AS s
    JOIN ml_training.clients c ON c.client_key = 1 + ((s * 37) % 500)
), scenarios AS (
    SELECT
        *,
        date_emission + delai_contractuel AS date_echeance,
        CASE
            WHEN facture_key % 20 = 0 THEN NULL
            WHEN ((facture_key * 29) % 100) <
                (risque_comportement * 100 + CASE WHEN montant_ttc > 12000 THEN 12 ELSE 0 END)
                THEN TRUE
            ELSE FALSE
        END AS est_en_retard
    FROM base
)
INSERT INTO ml_training.factures (
    facture_key, client_key, date_emission, date_echeance, date_paiement,
    montant_ttc, mode_paiement, statut
)
SELECT
    facture_key,
    client_key,
    date_emission,
    date_echeance,
    CASE
        WHEN est_en_retard IS NULL THEN NULL
        WHEN est_en_retard THEN date_echeance + (3 + ((facture_key * 7) % 43))::INTEGER
        ELSE date_echeance - (facture_key % 7)::INTEGER
    END,
    montant_ttc,
    mode_paiement,
    CASE WHEN est_en_retard IS NULL THEN 'EnAttente' ELSE 'Payee' END
FROM scenarios;

INSERT INTO ml_training.paiements (
    paiement_key, facture_key, montant, date_paiement, banque
)
SELECT
    facture_key,
    facture_key,
    montant_ttc,
    date_paiement,
    (ARRAY['BIAT', 'BNA', 'STB', 'Amen Bank'])[(facture_key % 4) + 1]
FROM ml_training.factures
WHERE date_paiement IS NOT NULL;

CREATE OR REPLACE VIEW ml_training.retard_features AS
WITH factures_et_label AS (
    SELECT
        f.facture_key,
        f.client_key,
        f.date_emission,
        f.date_echeance,
        f.date_paiement,
        f.montant_ttc,
        f.mode_paiement,
        c.type_client,
        c.ville,
        c.anciennete_mois,
        (f.date_paiement > f.date_echeance)::INTEGER AS retard
    FROM ml_training.factures f
    JOIN ml_training.clients c ON c.client_key = f.client_key
    WHERE f.date_paiement IS NOT NULL
), avec_historique AS (
    SELECT
        *,
        COUNT(*) OVER historique AS nombre_factures_precedentes,
        COUNT(*) FILTER (WHERE retard = 1) OVER historique AS nombre_retards_precedents,
        AVG(retard) OVER historique AS taux_retard_precedent,
        AVG((date_paiement - date_echeance)) OVER historique AS retard_moyen_jours
    FROM factures_et_label
    WINDOW historique AS (
        PARTITION BY client_key
        ORDER BY date_emission, facture_key
        ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
    )
)
SELECT
    facture_key,
    client_key,
    date_emission,
    montant_ttc,
    (date_echeance - date_emission) AS jours_delai,
    mode_paiement,
    type_client,
    ville,
    anciennete_mois,
    COALESCE(nombre_factures_precedentes, 0)::INTEGER AS nombre_factures_precedentes,
    COALESCE(nombre_retards_precedents, 0)::INTEGER AS nombre_retards_precedents,
    COALESCE(taux_retard_precedent, 0)::NUMERIC(8,5) AS taux_retard_precedent,
    COALESCE(retard_moyen_jours, 0)::NUMERIC(8,3) AS retard_moyen_jours,
    retard
FROM avec_historique;

INSERT INTO ml_training.generation_runs (clients_count, invoices_count, notes)
VALUES (
    500,
    10000,
    'Jeu synthetique reproductible tunisien pour le prototype de prediction de retard; aucune donnee du schema public n''est utilisee comme donnee d''entrainement.'
);



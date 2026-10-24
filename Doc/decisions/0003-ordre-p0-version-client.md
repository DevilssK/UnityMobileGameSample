# 0003 — Ordre : P0 backend, puis version Unity/SDK, puis client d'achat

Date : 2026-10-24 · Statut : acceptée

## Problème
Le client d'achat utilise l'API Unity IAP 4 (`IDetailedStoreListener`, `ConfigurationBuilder`), remplacée en IAP 5 (StoreKit 2 sur iOS). Corriger le client sur IAP 4 puis migrer double le travail ; le format du payload Apple côté serveur dépend aussi de ce choix. Unity `6000.1.7f1` du sample n'est pas installé localement.

## Décision
1. Corriger d'abord les P0 backend indépendants du SDK : suppression de `fake` en production, propriétaire unique de l'attribution (pas de `GrantRewards` après redeem), reprise idempotente, course de `HandleFreeCoinPackPurchase`.
2. Figer la version Unity et la matrice SDK (skill `sdk-upgrade`), dans des commits séparés.
3. Réécrire le flux d'achat client sur l'API retenue, puis l'adaptateur Apple serveur selon le format réel.

## Alternatives écartées
- Suivre le brief tel quel (P0 sur IAP 4) — double travail côté client.
- Monter la version avant tout — repousse la suppression de failles serveur indépendantes du SDK.

## Conséquences
- Les P1 de `StoreClient` (état d'achat, logs de reçus) sont traités lors de la réécriture client, pas sur IAP 4.

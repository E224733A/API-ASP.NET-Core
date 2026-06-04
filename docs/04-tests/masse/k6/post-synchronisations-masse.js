import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const schemaVersion = '1.3';
const trajetCamionInclus = true;
const apiBaseUrl = (__ENV.API_BASE_URL || 'http://localhost:5120').replace(/\/$/, '');
const dateTournee = __ENV.DATE_TOURNEE || failConfiguration('DATE_TOURNEE manquant. Utiliser le lanceur PowerShell.');
const runId = __ENV.RUN_ID || String(Date.now());
const codeTourneePrefix = __ENV.CODE_TOURNEE_PREFIX || `K6${runId.slice(-6)}`;
const lineCount = Number(__ENV.LINE_COUNT || '5');
const thinkTimeSeconds = Number(__ENV.THINK_TIME_SECONDS || '0.05');
const syncCount = Number(__ENV.SYNC_COUNT || '20');
const vus = Number(__ENV.VUS || '5');
const responseP95ThresholdMs = Number(__ENV.RESPONSE_P95_THRESHOLD_MS || '5000');

export const options = {
  scenarios: {
    synchronisations_mobiles: {
      executor: 'shared-iterations',
      vus,
      iterations: syncCount,
      maxDuration: __ENV.MAX_DURATION || '3m'
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: [`p(95)<${responseP95ThresholdMs}`],
    checks: ['rate>0.99'],
    mobile_sync_unexpected: ['count==0'],
    mobile_sync_success_200: [`count==${syncCount}`]
  }
};

const success200 = new Counter('mobile_sync_success_200');
const validation400 = new Counter('mobile_sync_validation_400');
const conflict409 = new Counter('mobile_sync_conflict_409');
const serverError500 = new Counter('mobile_sync_server_error_500');
const unexpected = new Counter('mobile_sync_unexpected');

function failConfiguration(message) {
  throw new Error(message);
}

function pad(value, size) {
  return String(value).padStart(size, '0');
}

function numericRunPart() {
  const digits = runId.replace(/\D/g, '');
  return digits.length === 0 ? '10000000' : digits.slice(-8).padStart(8, '0');
}

function guidFor(index) {
  const first = numericRunPart().slice(-8);
  const tailNumber = Number(numericRunPart().slice(-6)) * 1000 + index + 1;
  const tail = tailNumber.toString(16).padStart(12, '0').slice(-12);
  return `${first}-0000-4000-8000-${tail}`;
}

function isoDayOfWeek(dateIso) {
  const date = new Date(`${dateIso}T12:00:00Z`);
  const day = date.getUTCDay();
  return day === 0 ? 7 : day;
}

function dayLabel(day) {
  const labels = {
    1: 'Lundi',
    2: 'Mardi',
    3: 'Mercredi',
    4: 'Jeudi',
    5: 'Vendredi',
    6: 'Samedi',
    7: 'Dimanche'
  };
  return labels[day] || 'Jour inconnu';
}

function buildTrajet(sequence) {
  const numero = pad(sequence, 3);
  const kilometrageDepart = 100000 + sequence * 100;
  const kilometrageArrivee = kilometrageDepart + 50 + lineCount;

  return {
    camion: {
      idCamion: `K6-CAMION-${numero}`,
      codeCamion: `K6-${numero}`,
      libelleCamion: `Camion test masse ${numero}`,
      immatriculation: `K6-${numero}`
    },
    kilometrageDepart,
    kilometrageArrivee,
    dateDepartMobile: `${dateTournee}T07:45:00+02:00`,
    dateArriveeMobile: `${dateTournee}T16:45:00+02:00`
  };
}

function buildQuantites(lineIndex) {
  return [
    {
      codeArticle: 'ROLLS',
      libelle: 'Rolls',
      quantiteLivreePrevue: 1 + (lineIndex % 3),
      quantiteLivree: 1 + (lineIndex % 3),
      quantiteRecuperee: lineIndex % 2
    },
    {
      codeArticle: 'ROLLS_VIDES',
      libelle: 'Chariots vides',
      quantiteLivreePrevue: null,
      quantiteLivree: lineIndex % 2,
      quantiteRecuperee: 1 + (lineIndex % 4)
    },
    {
      codeArticle: 'TAPIS',
      libelle: 'Tapis',
      quantiteLivreePrevue: lineIndex % 2,
      quantiteLivree: lineIndex % 2,
      quantiteRecuperee: 0
    },
    {
      codeArticle: 'SACS',
      libelle: 'Sacs',
      quantiteLivreePrevue: 0,
      quantiteLivree: lineIndex % 3 === 0 ? 1 : 0,
      quantiteRecuperee: lineIndex % 3 === 1 ? 1 : 0
    }
  ];
}

function buildPayload(index) {
  const sequence = index + 1;
  const codeTournee = `${codeTourneePrefix}${pad(sequence, 3)}`;
  const jourTournee = isoDayOfWeek(dateTournee);
  const jourLibelle = dayLabel(jourTournee);
  const lignes = [];

  for (let lineIndex = 0; lineIndex < lineCount; lineIndex += 1) {
    const clientNumber = `${7000 + sequence * 10 + lineIndex}`;
    const pdlCode = `PDL-K6-${pad(sequence, 3)}-${pad(lineIndex + 1, 2)}`;
    const ordre = lineIndex + 1;

    lignes.push({
      idLigneSource: `${dateTournee}|${codeTournee}|${jourTournee}|${clientNumber}|${pdlCode}|${ordre}`,
      ordreArret: ordre,
      horaire: String(ordre),
      client: {
        numClient: clientNumber,
        nomClient: `CLIENT TEST MASSE ${pad(sequence, 3)}-${pad(lineIndex + 1, 2)}`,
        nomAffiche: `CLIENT TEST MASSE ${pad(sequence, 3)}-${pad(lineIndex + 1, 2)}`
      },
      pointLivraison: {
        codePDL: pdlCode,
        descriptionPDL: `Point test masse ${pad(sequence, 3)}-${pad(lineIndex + 1, 2)}`,
        adresseLigne1: `${ordre} RUE DU TEST DE MASSE`,
        adresseLigne2: null,
        adresseLigne3: null,
        ville: 'NANTES',
        codePostal: '44000'
      },
      tournee: {
        codeTournee,
        libelleTournee: `TEST MASSE ${runId}`,
        jourTournee,
        jourLibelle,
        schemaLivraison: '1W1'
      },
      retour: {
        jourTourneeRetour: jourTournee,
        jourRetourLibelle: jourLibelle,
        codeTourneeRetour: codeTournee,
        libelleTourneeRetour: `TEST MASSE ${runId}`
      },
      infosLivreur: {
        instructions: lineIndex % 2 === 0 ? 'Instruction test masse' : null,
        commentaireExceptionnel: null,
        zoneDechargement: lineIndex % 2 === 0 ? 'EHPAD' : null,
        zoneDechargementAffichee: lineIndex % 2 === 0 ? 'EHPAD' : String(jourTournee),
        zone: null,
        precision: null,
        cle: null,
        estFerme: false,
        dateFermeture: null,
        motifFermeture: null
      },
      saisie: {
        precisionLivreur: `Test k6 masse ${runId}`,
        statutPassage: 'FAIT',
        commentaireLivreur: null,
        heureValidation: `${dateTournee}T09:${pad((10 + lineIndex) % 60, 2)}:00+02:00`,
        estValidee: true,
        quantites: buildQuantites(lineIndex)
      }
    });
  }

  return {
    schemaVersion,
    idSynchronisation: guidFor(index),
    dateTournee,
    codeTournee,
    libelleTournee: `TEST MASSE ${runId}`,
    livreur: {
      codeLivreur: 'K6',
      nomLivreur: 'LIVREUR TEST MASSE'
    },
    mobile: {
      nomAppareil: `k6-${runId}`,
      versionApplication: '1.0.0-k6',
      dateChargementMobile: `${dateTournee}T07:30:00+02:00`,
      dateEnvoiMobile: `${dateTournee}T16:45:00+02:00`
    },
    trajet: buildTrajet(sequence),
    commentaireGlobal: `Test de masse k6 ${runId}`,
    lignes
  };
}

export default function () {
  const index = exec.scenario.iterationInTest;
  const payload = buildPayload(index);
  const response = http.post(
    `${apiBaseUrl}/api/synchronisations`,
    JSON.stringify(payload),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Test-Run-Id': runId,
        'X-Test-Type': 'MobileSLI-k6-masse'
      },
      tags: {
        test_type: 'mobile_synchronisation_masse',
        run_id: runId,
        code_tournee_prefix: codeTourneePrefix
      }
    }
  );

  if (response.status === 200) {
    success200.add(1);
  } else if (response.status === 400) {
    validation400.add(1);
  } else if (response.status === 409) {
    conflict409.add(1);
  } else if (response.status >= 500) {
    serverError500.add(1);
    unexpected.add(1);
  } else {
    unexpected.add(1);
  }

  check(response, {
    'HTTP 200 attendu pour synchronisation valide': (res) => res.status === 200,
    'réponse JSON exploitable': (res) => {
      try {
        const body = res.json();
        return body && body.statut === 'SUCCESS';
      } catch (error) {
        return false;
      }
    }
  });

  sleep(thinkTimeSeconds);
}

function metricValue(data, metricName, valueName) {
  const metric = data.metrics && data.metrics[metricName];
  if (!metric || !metric.values) {
    return null;
  }
  return metric.values[valueName];
}

function formatNumber(value, decimals = 2) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) {
    return 'n/a';
  }
  return Number(value).toFixed(decimals);
}

function buildMarkdownSummary(data) {
  const httpReqs = metricValue(data, 'http_reqs', 'count');
  const avg = metricValue(data, 'http_req_duration', 'avg');
  const p95 = metricValue(data, 'http_req_duration', 'p(95)');
  const checksRate = metricValue(data, 'checks', 'rate');
  const failedRate = metricValue(data, 'http_req_failed', 'rate');
  const successes = metricValue(data, 'mobile_sync_success_200', 'count') || 0;
  const validations = metricValue(data, 'mobile_sync_validation_400', 'count') || 0;
  const conflicts = metricValue(data, 'mobile_sync_conflict_409', 'count') || 0;
  const serverErrors = metricValue(data, 'mobile_sync_server_error_500', 'count') || 0;
  const unexpectedCount = metricValue(data, 'mobile_sync_unexpected', 'count') || 0;

  const success = Number(successes) === syncCount && Number(unexpectedCount) === 0 && Number(serverErrors) === 0;

  return `# Rapport k6 - synchronisations mobiles de masse\n\n` +
    `RunId : \`${runId}\`\n\n` +
    `Date tournée : \`${dateTournee}\`\n\n` +
    `Préfixe tournées : \`${codeTourneePrefix}\`\n\n` +
    `API : \`${apiBaseUrl}\`\n\n` +
    `Seuil p95 : \`${responseP95ThresholdMs} ms\`\n\n` +
    `## Paramètres\n\n` +
    `| Paramètre | Valeur |\n` +
    `|---|---:|\n` +
    `| SchemaVersion utilisée | ${schemaVersion} |\n` +
    `| Trajet camion inclus | ${trajetCamionInclus ? 'oui' : 'non'} |\n` +
    `| Synchronisations attendues | ${syncCount} |\n` +
    `| VUs k6 | ${vus} |\n` +
    `| Lignes par tournée | ${lineCount} |\n` +
    `| Articles par ligne | 4 |\n\n` +
    `## Résultats k6\n\n` +
    `| Indicateur | Valeur |\n` +
    `|---|---:|\n` +
    `| Requêtes HTTP | ${formatNumber(httpReqs, 0)} |\n` +
    `| Synchronisations attendues | ${syncCount} |\n` +
    `| Succès HTTP 200 | ${formatNumber(successes, 0)} |\n` +
    `| Validations HTTP 400 | ${formatNumber(validations, 0)} |\n` +
    `| Conflits HTTP 409 | ${formatNumber(conflicts, 0)} |\n` +
    `| Erreurs serveur 500 | ${formatNumber(serverErrors, 0)} |\n` +
    `| Réponses inattendues | ${formatNumber(unexpectedCount, 0)} |\n` +
    `| Temps moyen HTTP | ${formatNumber(avg)} ms |\n` +
    `| p95 HTTP | ${formatNumber(p95)} ms |\n` +
    `| Taux de checks OK | ${formatNumber(Number(checksRate) * 100)} % |\n` +
    `| Taux de requêtes échouées k6 | ${formatNumber(Number(failedRate) * 100)} % |\n\n` +
    `## Conclusion automatique\n\n` +
    (success
      ? `Le test de masse est réussi côté API : toutes les synchronisations valides attendues ont été acceptées et aucune erreur serveur n'a été détectée. La vérification SQL doit confirmer les volumes sauvegardés, y compris dbo.Mobile_TourneeCamion.\n`
      : `Le test de masse nécessite une analyse : le nombre de succès ou d'erreurs ne correspond pas au résultat attendu. Vérifier le journal console, le résumé JSON et la base SQL.\n`);
}

export function handleSummary(data) {
  return {
    [`resultats/k6-summary-${runId}.json`]: JSON.stringify(data, null, 2),
    [`rapports/rapport-k6-masse-${runId}.md`]: buildMarkdownSummary(data),
    stdout: buildMarkdownSummary(data)
  };
}

import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const apiBaseUrl = (__ENV.API_BASE_URL || 'http://localhost:5120').replace(/\/$/, '');
const dateTournee = __ENV.DATE_TOURNEE || failConfiguration('DATE_TOURNEE manquant. Utiliser le lanceur PowerShell ou définir la variable.');
const runId = __ENV.RUN_ID || String(Date.now());
const codeTourneePrefix = __ENV.CODE_TOURNEE_PREFIX || `K6M${runId.slice(-5)}`;
const syncCount = Number(__ENV.SYNC_COUNT || '20');
const vus = Number(__ENV.VUS || '5');

export const options = {
  scenarios: {
    synchronisations_mixtes: {
      executor: 'shared-iterations',
      vus,
      iterations: syncCount,
      maxDuration: __ENV.MAX_DURATION || '3m'
    }
  },
  thresholds: {
    http_req_duration: ['p(95)<2500'],
    checks: ['rate>0.95'],
    mobile_sync_unexpected: ['count==0']
  }
};

const success200 = new Counter('mobile_sync_success_200');
const expected400 = new Counter('mobile_sync_expected_validation_400');
const unexpected = new Counter('mobile_sync_unexpected');

function failConfiguration(message) {
  throw new Error(message);
}

function pad(value, size) {
  return String(value).padStart(size, '0');
}

function guidFor(index) {
  const digits = runId.replace(/\D/g, '').slice(-8).padStart(8, '0');
  const tail = (Number(digits.slice(-6)) * 1000 + index + 1).toString(16).padStart(12, '0').slice(-12);
  return `${digits}-0000-4000-8000-${tail}`;
}

function buildPayload(index) {
  const sequence = index + 1;
  const codeTournee = `${codeTourneePrefix}${pad(sequence, 3)}`;
  const isInvalid = sequence % 5 === 0;
  const quantiteLivree = isInvalid ? -1 : 1;

  return {
    schemaVersion: '1.2',
    idSynchronisation: guidFor(index),
    dateTournee,
    codeTournee,
    libelleTournee: `TEST MASSE MIXTE ${runId}`,
    livreur: {
      codeLivreur: 'K6',
      nomLivreur: 'LIVREUR TEST MASSE'
    },
    mobile: {
      nomAppareil: `k6-mixte-${runId}`,
      versionApplication: '1.0.0-k6',
      dateChargementMobile: `${dateTournee}T07:30:00+02:00`,
      dateEnvoiMobile: `${dateTournee}T16:45:00+02:00`
    },
    commentaireGlobal: `Test de masse mixte k6 ${runId}`,
    lignes: [
      {
        idLigneSource: `${dateTournee}|${codeTournee}|1|${8000 + sequence}|PDL-MIXTE-${pad(sequence, 3)}|1`,
        ordreArret: 1,
        horaire: '1',
        client: {
          numClient: `${8000 + sequence}`,
          nomClient: `CLIENT TEST MIXTE ${pad(sequence, 3)}`,
          nomAffiche: `CLIENT TEST MIXTE ${pad(sequence, 3)}`
        },
        pointLivraison: {
          codePDL: `PDL-MIXTE-${pad(sequence, 3)}`,
          descriptionPDL: `Point test mixte ${pad(sequence, 3)}`,
          adresseLigne1: '1 RUE DU TEST MIXTE',
          adresseLigne2: null,
          adresseLigne3: null,
          ville: 'NANTES',
          codePostal: '44000'
        },
        tournee: {
          codeTournee,
          libelleTournee: `TEST MASSE MIXTE ${runId}`,
          jourTournee: 1,
          jourLibelle: 'Lundi',
          schemaLivraison: '1W1'
        },
        retour: {
          jourTourneeRetour: 1,
          jourRetourLibelle: 'Lundi',
          codeTourneeRetour: codeTournee,
          libelleTourneeRetour: `TEST MASSE MIXTE ${runId}`
        },
        infosLivreur: {
          instructions: null,
          commentaireExceptionnel: null,
          zoneDechargement: null,
          zoneDechargementAffichee: '1',
          zone: null,
          precision: null,
          cle: null,
          estFerme: false,
          dateFermeture: null,
          motifFermeture: null
        },
        saisie: {
          precisionLivreur: `Test k6 mixte ${runId}`,
          statutPassage: 'FAIT',
          commentaireLivreur: null,
          heureValidation: `${dateTournee}T09:12:00+02:00`,
          estValidee: true,
          quantites: [
            {
              codeArticle: 'ROLLS',
              libelle: 'Rolls',
              quantiteLivreePrevue: null,
              quantiteLivree,
              quantiteRecuperee: 0
            }
          ]
        }
      }
    ]
  };
}

export default function () {
  const index = exec.scenario.iterationInTest;
  const sequence = index + 1;
  const shouldBeInvalid = sequence % 5 === 0;
  const payload = buildPayload(index);

  const response = http.post(`${apiBaseUrl}/api/synchronisations`, JSON.stringify(payload), {
    headers: {
      'Content-Type': 'application/json',
      'X-Test-Run-Id': runId,
      'X-Test-Type': 'MobileSLI-k6-mixte'
    }
  });

  if (!shouldBeInvalid && response.status === 200) {
    success200.add(1);
  } else if (shouldBeInvalid && response.status === 400) {
    expected400.add(1);
  } else {
    unexpected.add(1);
  }

  check(response, {
    'réponse conforme au cas attendu': (res) => shouldBeInvalid ? res.status === 400 : res.status === 200
  });

  sleep(0.05);
}

function metricValue(data, metricName, valueName) {
  const metric = data.metrics && data.metrics[metricName];
  if (!metric || !metric.values) {
    return null;
  }
  return metric.values[valueName];
}

function report(data) {
  const successes = metricValue(data, 'mobile_sync_success_200', 'count') || 0;
  const expectedErrors = metricValue(data, 'mobile_sync_expected_validation_400', 'count') || 0;
  const unexpectedCount = metricValue(data, 'mobile_sync_unexpected', 'count') || 0;
  const p95 = metricValue(data, 'http_req_duration', 'p(95)');

  return `# Rapport k6 - synchronisations mixtes\n\n` +
    `RunId : \`${runId}\`\n\n` +
    `Date tournée : \`${dateTournee}\`\n\n` +
    `Préfixe tournées : \`${codeTourneePrefix}\`\n\n` +
    `## Résultats\n\n` +
    `| Indicateur | Valeur |\n` +
    `|---|---:|\n` +
    `| Succès HTTP 200 attendus | ${successes} |\n` +
    `| Erreurs HTTP 400 attendues | ${expectedErrors} |\n` +
    `| Réponses inattendues | ${unexpectedCount} |\n` +
    `| p95 HTTP | ${p95 === null ? 'n/a' : Number(p95).toFixed(2)} ms |\n\n` +
    `Ce scénario est complémentaire. Pour la preuve SQL principale, utiliser plutôt le scénario valide de masse.\n`;
}

export function handleSummary(data) {
  return {
    [`resultats/k6-summary-mixte-${runId}.json`]: JSON.stringify(data, null, 2),
    [`rapports/rapport-k6-mixte-${runId}.md`]: report(data),
    stdout: report(data)
  };
}

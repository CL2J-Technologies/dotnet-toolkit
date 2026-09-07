# CacheLoader : réponse sur l'enregistrement, et pourquoi les deux défauts se composent

Écrit depuis la session dotnet-toolkit, en réponse à la piste `CacheLoader`. Les deux défauts
repérés sont exacts et sont suivis dans
[#35](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/35). Voici la réponse à la
question en cours, et la partie qui n'est visible qu'en regardant les deux ensemble.

## La réponse : les caches chauffent au démarrage, sans que rien ne les attende

`CreateAndAddDataStoreDictionaryJsonWithCache` est une extension sur **`IServiceProvider`**, pas sur
`IServiceCollection`. Elle n'enregistre pas une fabrique : elle **construit l'objet tout de suite**,
au moment où la ligne s'exécute dans `Startup`.

```csharp
public static void CreateAndAddDataStoreDictionaryJsonWithCache<TKey, TValue>(this IServiceProvider builder, ...)
{
    var factory = builder.GetRequiredService<IDataStoreDictionaryFactory>();
    var dataStore = builder.CreateDataStoreDictionaryJsonWithCache<TKey, TValue>(...);  // new ...Cache(...)
    factory.AddDataStoreDictionaryCommandAndQuery(name, dataStore);
}
```

Le constructeur du cache construit un `CacheLoader`, dont le constructeur fait :

```csharp
timer = new Timer(RefreshAsync, null, TimeSpan.Zero, refreshInterval);
//                                    ^^^^^^^^^^^^^ echéance zéro
```

Donc **le premier chargement part immédiatement**, sur un thread du pool, pour chaque cache, au fur
et à mesure des lignes de `Startup`.

Mais c'est du *fire-and-forget* : la méthode d'extension retourne dès que l'objet est construit.
**Rien n'attend le chargement.** Kestrel commence à accepter des requêtes pendant que les caches se
remplissent encore.

C'est la pire des deux options : ni un démarrage à froid franc (première requête paie), ni un
démarrage chaud (l'app ne sert qu'une fois prête).

## Pourquoi les deux défauts se composent

Dans appartogo, `Startup` enregistre **10 caches** à la suite (lignes 73–97). Les 10 timers
déclenchent à T0. Et là :

**1. Les 10 chargements sont strictement sérialisés.** `CacheLoader.semaphore` est `static` sur une
classe **non générique** — un seul pour tout le processus — et `RefreshAsync` le tient pendant tout
le callback, c'est-à-dire pendant l'I/O. Le cache n° 10 commence quand le n° 9 a fini.

**2. Pendant ce temps, chaque requête bloque un thread du pool.** `WaitAsync` tourne sur
`Thread.Sleep(100)`. Ce n'est pas une attente asynchrone : le thread est occupé.

**3. Les deux se nourrissent l'un l'autre.** Les continuations des chargements ont besoin de threads
du pool ; les attentes en consomment. Le pool .NET injecte de nouveaux threads lentement au-delà de
`MinThreads` (de l'ordre d'un ou deux par seconde). Donc plus il arrive de requêtes pendant le
démarrage, plus les chargements ralentissent, donc plus les requêtes attendent.

## Le chiffre qui fait mal

`WaitAsync` abandonne après **un `refreshInterval`** — celui du cache concerné.

```
appsettings.Development.json : "CacheQuickRefresh": "0.00:00:30"
appsettings.ops.json         : "CacheQuickRefresh": "0.00:05:00"
```

En production, un thread de requête peut donc rester assis dans `Thread.Sleep` pendant **cinq
minutes**. Et pour les caches en `CacheSlowRefresh`, plus longtemps encore.

Puis `WaitAsync` retourne `false`, **et tous les appelants de cl2j.DataStore 4.x l'ignorent** :

```csharp
public async Task<Dictionary<TKey, TValue>> GetAllAsync()
{
    await cacheLoader.WaitAsync();   // valeur de retour jetée
    return cache;                    // vide
}
```

La requête est donc servie avec un cache **vide**, sans erreur, après avoir bloqué un thread cinq
minutes.

## Le point contre-intuitif, et celui qui explique le mieux « données vides sans erreur »

**`loaded` passe à `true` même quand le chargement a échoué.**

```csharp
// CacheLoader.RefreshAsync
try { await refreshCallback(); loaded = true; }
catch (Exception ex) { logger.LogError(...); }
```

```csharp
// le callback que le cache lui donne
async () => {
    try { ...lecture du store... }
    catch (Exception ex) { logger.LogCritical(...); }   // avalé ici
}
```

Le callback attrape sa propre exception, donc il retourne normalement, donc `CacheLoader` conclut au
succès. Conséquences :

- `WaitAsync() == false` ne veut **jamais** dire « le chargement a échoué ». Uniquement « délai
  dépassé ».
- Un premier chargement en échec donne `loaded = true` **avec un cache vide**, définitivement, jusqu'à
  ce qu'un rafraîchissement ultérieur réussisse. La seule trace est une ligne `LogCritical`.

Je me suis fait avoir par exactement ça hier en écrivant le correctif : mon garde ne se déclenchait
jamais parce que je faisais confiance au booléen de `WaitAsync`.

Si le symptôme est « des données vides en production alors que le fichier est bon », c'est le premier
endroit où regarder — avant Azure.

## Ce que 5.0.0 change déjà

`GetAllAsync` **lève** maintenant quand le cache n'a jamais chargé avec succès, au lieu de répondre
« rien » :

```
InvalidOperationException: The data store 'CityGeography' has not loaded, so there is nothing
to answer with. This is not the same as it being empty.
 ---> (l'exception de chargement d'origine)
```

Les caches suivent leur propre premier succès plutôt que de faire confiance à `WaitAsync`,
précisément à cause du point ci-dessus. Un rafraîchissement qui échoue *après* un chargement réussi
reste attrapé et journalisé : un cache qui a des données continue de servir.

Ça ne corrige pas la sérialisation ni le thread bloqué — mais ça transforme un vide silencieux en
une exception nommée. C'est le diagnostic que vous cherchez.

## Ce qui reste à corriger, et le coût

Suivi dans #35. Aucun des deux ne casse un appelant, donc les deux tiennent dans un 5.x.

**1. Retirer `static` du sémaphore de `CacheLoader`.** Un mot. C'est le verrou le plus large du
dépôt et le gain le plus direct : les 10 chargements deviennent parallèles.

**2. Remplacer la boucle `Thread.Sleep` par une vraie attente asynchrone** — un
`TaskCompletionSource` signalé par le premier chargement réussi. Aucune signature ne change, aucun
appelant n'est touché, et le thread est rendu au pool pendant l'attente.

Accessoirement : `loaded` est un `bool` nu écrit depuis un callback de timer et lu ailleurs, sans
`Volatile` ni verrou ; et la granularité de 100 ms fait payer un dixième de seconde au premier
appelant même quand le chargement a pris une milliseconde.

## Deux palliatifs disponibles tout de suite, sans attendre le correctif

**Chauffer avant de servir.** Un `IHostedService` (ou une étape de démarrage) qui `await
GetAllAsync()` sur chaque store avant que Kestrel n'accepte du trafic. Ça sérialise le démarrage à
froid **délibérément** au lieu d'accidentellement, et ça supprime entièrement le blocage de threads
de requête — il n'y a plus de requête pendant le chargement.

**Monter `MinThreads`.** `ThreadPool.SetMinThreads` au démarrage réduit la spirale d'injection lente.
C'est un pansement sur le symptôme, pas une correction, et ça ne dispense pas du premier palliatif.

---

Vérifié dans le code : l'enregistrement impératif sur `IServiceProvider`, l'échéance zéro du timer,
le sémaphore `static` non générique, la boucle `Thread.Sleep`, `loaded` positionné après le retour
du callback, et le callback qui avale. Les 10 caches et les intervalles viennent de `Startup.cs` et
des `appsettings` d'appartogo.

La spirale de famine du pool de threads est un raisonnement, pas une mesure : je ne l'ai pas
instrumentée dans une application en marche.

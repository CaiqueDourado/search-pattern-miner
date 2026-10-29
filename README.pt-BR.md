# search-pattern-miner

[English](README.md)

[![Python](https://github.com/CaiqueDourado/search-pattern-miner/actions/workflows/python.yml/badge.svg)](https://github.com/CaiqueDourado/search-pattern-miner/actions/workflows/python.yml)
[![.NET](https://github.com/CaiqueDourado/search-pattern-miner/actions/workflows/dotnet.yml/badge.svg)](https://github.com/CaiqueDourado/search-pattern-miner/actions/workflows/dotnet.yml)
![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue)

![search-pattern-miner](docs/img/cover.pt-BR.png)

Transforma buscas em padrões reaproveitáveis, para que milhares de buscas virem um modelo de página:

```text
como fazer panqueca          ┐
como fazer pudim             ├─ com faz [comida]
como fazer bolo de cenoura   ┘

apartamento para alugar em curitiba  ┐
apartamento para alugar em goiânia   ┴─ [imovel] par alug [cidade]
```

São os sete algoritmos que o autor escreveu em C# entre 2017 e 2019 para escolher quais dos seus ~80 sites de SEO
programático construir, e que depois sustentaram o [SuperNichos.com](https://supernichos.com). Cada um tem um nome
técnico e, como no código original, um sobrenome como apelido. A biblioteca roda em **Python e .NET com o mesmo
resultado**, conferido pelos mesmos 8.801 valores de [`testdata/golden.json`](testdata/golden.json), e não tem
dependências em nenhuma das duas.

| Algoritmo | Apelido | O que guarda | `como fazer bolo de cenoura` |
|---|---|---|---|
| `prefix` | Viana | as primeiras 2 a 5 palavras | `com faz` · `com faz bol` · `com faz bol cenour` |
| `contiguous` | Vilela | toda sequência de 2+ palavras que não seja prefixo | `faz bol` · `bol cenour` · `faz bol cenour` |
| `skipgram` | Oliveira | 2 a 4 palavras na ordem, com saltos | `com bol` · `faz cenour` · `com faz cenour` … |
| `bag` | Dourado | 2 a 4 palavras diferentes, em qualquer ordem | `bol com` · `cenour faz` · `bol cenour com faz` … |
| `entity_prefix` | Barbosa | `prefix` depois de as entidades virarem tipos | `com faz [comida]` |
| `entity_contiguous` | Figueredo | `contiguous` depois de as entidades virarem tipos | `faz [comida]` |
| `entity_skipgram` | Rosa | `skipgram` depois de as entidades virarem tipos | `com [comida]` |

As palavras são reduzidas ao radical (*previsão* → `previsa`, *como* → `com`) por uma porta do stemmer brasileiro do
Lucene, o mesmo que o código original usava; por isso os padrões soam estranhos, mas agrupam bem. Cada algoritmo só
guarda o que os anteriores ainda não produziram para a mesma busca (a cadeia de exclusão).

## Instalação

```bash
pip install "search-pattern-miner @ git+https://github.com/CaiqueDourado/search-pattern-miner#subdirectory=python"
```

```bash
dotnet add package SearchPatternMiner   # quando publicado; por enquanto, referencie dotnet/src/SearchPatternMiner
```

## Uso

```python
from search_pattern_miner import Gazetteer, Search, aggregate, mine

gaz = Gazetteer.load("data/gazetteer-pt.tsv")      # forma<TAB>tipos, ex.: "curitiba\tLocal > Cidade"
mine("hotéis baratos em salvador", gaz, ["barbosa", "rosa"])
# {'entity_prefix': ['[turismo] barat', '[turismo] barat [local]', '[turismo] barat [cidade]'],
#  'entity_skipgram': ['[turismo] [local]', '[turismo] [cidade]']}

searches = [Search("como fazer pudim", volume=8100, competition=0.2, cpc=0.5),
            Search("como fazer panqueca", volume=49500, competition=0.7, cpc=2.1)]
for p in aggregate(searches, gaz, ["barbosa"]):
    print(p.pattern, p.num_searches, p.volume_total, p.top_search, p.band_volume)
# com faz [comida] 2 57600 como fazer panqueca 1
```

```csharp
using SearchPatternMiner;

var gaz = Gazetteer.Load("data/gazetteer-pt.tsv");
var patterns = Miner.Mine("como fazer panqueca", gaz, new[] { "barbosa" });   // entity_prefix: com faz [comida]
var top = Aggregator.Aggregate(searches, gaz, new[] { "entity_prefix" });
```

```bash
search-pattern-miner show "detran pe" --gazetteer data/gazetteer-pt.tsv
search-pattern-miner mine examples/keywords.csv --gazetteer data/gazetteer-pt.tsv --out patterns.csv --pairs pairs.csv
```

**Uma busca, os sete.** O `show` imprime o que cada algoritmo faz com uma busca, depois da cadeia de exclusão:

```bash
search-pattern-miner show "hotéis baratos em salvador" --gazetteer data/gazetteer-pt.tsv
```

```json
{
  "prefix (viana)": [
    "hot barat",
    "hot barat salvador"
  ],
  "contiguous (vilela)": [
    "barat salvador"
  ],
  "skipgram (oliveira)": [
    "hot salvador"
  ],
  "bag (dourado)": [
    "barat hot",
    "barat hot salvador"
  ],
  "entity_prefix (barbosa)": [
    "[turismo] barat",
    "[turismo] barat [local]",
    "[turismo] barat [cidade]"
  ],
  "entity_contiguous (figueredo)": [
    "barat [local]",
    "barat [cidade]"
  ],
  "entity_skipgram (rosa)": [
    "[turismo] [local]",
    "[turismo] [cidade]"
  ]
}
```

*hotéis* vira `hot` e *baratos* vira `barat` (radicais), *em* é stopword. Os quatro algoritmos de palavras veem
`salvador` como palavra; os três de entidades a veem como lugar e cidade (`Local > Cidade` no gazetteer), e *hotéis*
como `[turismo]`, então fazem um padrão por tipo. Cada lista deixa de fora o que os algoritmos acima já deram.

O `mine` lê um CSV com a coluna `keyword` e, se houver, `volume`, `competition` e `cpc` (os nomes mudam por opção, e
`--group market` ordena os padrões dentro de cada mercado). Ele grava uma linha por padrão com o número de buscas,
o volume total, as médias de concorrência e CPC, a busca de maior volume, três exemplos e quatro **faixas de 1 a 20**
(NTILE(20), os "vigintis" das telas de curadoria originais: faixa 1 = mais volume, mais buscas, menos concorrência,
CPC mais baixo).

## Como um padrão é estruturado

![Um padrão em JSON](docs/img/pattern-json.png)

`com faz [comida]` junta as 10 buscas "como fazer + comida" do arquivo de exemplo sintético: 119.370 buscas por mês,
primeira faixa de volume. Arquivo completo: [docs/example-pattern.json](docs/example-pattern.json), gerado por
[scripts/make_json.py](scripts/make_json.py).

## Entidades

O dicionário de entidades (gazetteer) é um arquivo de texto, uma entidade por linha: a forma escrita, um tab e os
tipos. `Local > Cidade` quer dizer que a entidade é um lugar e uma cidade, e **cada tipo gera um padrão** (o original
nunca escolhia um). A biblioteca traz um exemplo pequeno, [data/gazetteer-pt.tsv](data/gazetteer-pt.tsv): os 27
estados e suas siglas, os 300+ municípios com 100 mil habitantes ou mais (da base do autor
[municipios-e-estados-do-brasil](https://github.com/CaiqueDourado/municipios-e-estados-do-brasil)) e listas curtas,
feitas à mão, de comidas, loterias, tipos de imóvel, turismo, documentos, sites, órgãos, profissões e animais. Para
uso real, use o [portuguese-entity-gazetteer](https://github.com/CaiqueDourado/portuguese-entity-gazetteer): 307 mil entidades com seus tipos, neste formato
(`gazetteer-pt.tsv` no [Kaggle](https://www.kaggle.com/datasets/caiquedourado/portuguese-entity-gazetteer)), o dicionário que os algoritmos originais usavam, limpo.

## Dois modos

| | `mode="fixed"` (padrão) | `mode="legacy2019"` |
|---|---|---|
| Casamento de entidades | palavras inteiras, a mais longa em cada posição | troca de texto, por isso `pr` (Paraná) casava dentro de *previsão* |
| Acentos | ignorados em stopwords e entidades; palavras curtas como `pará`/`para` e `pe`/`pé` precisam casar exatamente | stopwords comparadas com acento |
| Marcadores | um token único | protegidos escrevendo o tipo como `c¨i¨d¨a¨d¨e` durante a troca |
| Padrões repetidos | cada padrão uma vez | duplicatas mantidas, como na `List<string>` original |
| Tokenizador | pontuação das pontas removida | dígitos e pontuação mantidos; palavra descartada deixa espaço duplo |

O modo legado existe para reproduzir os dados publicados. Nas linhas da amostra do
[google-keywords-dataset](https://github.com/CaiqueDourado/google-keywords-dataset), ele devolve todos os padrões
conferidos ([evaluation/reproduce.py](evaluation/reproduce.py), [resultados](evaluation/results.md)):

| Tabela publicada | Linhas conferidas | Reproduzidas |
|---|---:|---:|
| `padroes`, prefixos do Brasil (lote de 2018) | 921 | 921 |
| `padroes_algoritmos`, Viana | 400 | 400 |
| `padroes_algoritmos`, Vilela (feita antes da cadeia de exclusão) | 400 | 400 |

Com o dicionário de entidades original de 2019 (um retrato de 2022, não publicado como estava; a versão limpa é o
[portuguese-entity-gazetteer](https://github.com/CaiqueDourado/portuguese-entity-gazetteer)), Barbosa e Rosa também reproduzem 400 de 400 linhas publicadas, e Figueredo 351
(as demais usam a grafia de tipos de uma versão mais antiga do dicionário).

## Como funciona

```mermaid
flowchart LR
  K["busca<br/>como fazer panqueca"] --> T["tokenizador<br/>sem stopwords, radical do Lucene<br/>com faz panquec"]
  T --> V["prefix · Viana"] --> C["contiguous · Vilela"] --> S["skipgram · Oliveira"] --> B["bag · Dourado"]
  K --> E["gazetteer<br/>panqueca → [comida]"]
  E --> T2["tokenizador<br/>com faz [comida]"]
  T2 --> EP["entity_prefix · Barbosa"] --> EC["entity_contiguous · Figueredo"] --> ES["entity_skipgram · Rosa"]
  V & C & S & B & EP & EC & ES --> A["agregação<br/>buscas, volume, CPC,<br/>faixas NTILE(20)"]
```

Todas as regras, inclusive os bugs que o modo legado mantém de propósito, estão em
[spec/ALGORITHMS.md](spec/ALGORITHMS.md). Em resumo:

- **Tokenizador.** Minúsculas, as stopwords do NLTK em português menos oito palavras que importam em buscas (`para`,
  `mais`, `quando`, `como`, `muito`, `qual`, `pelo`, `pelos`), depois o radical. Palavras de uma ou duas letras somem
  (regra do Lucene), então `sp` e `tv` só chegam a um padrão se o gazetteer as transformar em entidade antes.
- **Cadeia de exclusão**, por busca: Vilela descarta os padrões da Viana, Oliveira descarta os dois, Dourado
  descarta os três; Figueredo descarta os do Barbosa, Rosa descarta os dois. Use `exclude=False` para ter todos os
  padrões de todos os algoritmos.
- **Expansão e colapso.** `[local>cidade]` vira um padrão com `[local]` e outro com `[cidade]`; `x [local] [local]`
  vira `x [local]`, mas um padrão feito só da entidade fica (`[local] [local]` era o segundo padrão Barbosa mais
  comum nos dados publicados).

## Cada algoritmo, passo a passo

Os sete começam pelo mesmo tokenizador; depois cada um gera seus padrões e descarta o que os anteriores já
produziram para a mesma busca (a cadeia de exclusão). Os exemplos são saídas reais da biblioteca com o gazetteer de
exemplo.

**Tokenizador** (comum a todos)

```mermaid
flowchart LR
  A["Como fazer bolo de cenoura"] --> B["minúsculas,<br/>sem acentos,<br/>sem pontuação nas pontas"]
  B --> C{"stopword?<br/>(NLTK menos para, mais,<br/>quando, como, muito,<br/>qual, pelo, pelos)"}
  C -- "sim: de" --> X["descartada"]
  C -- não --> E{"3 caracteres<br/>ou mais?"}
  E -- não --> X
  E -- sim --> D["stemmer brasileiro do Lucene<br/>como → com · fazer → faz<br/>bolo → bol · cenoura → cenour"]
  D --> F["tokens<br/>com faz bol cenour"]
```

**Viana · `prefix`**: como a busca *começa*. Bom para intenções (`com faz …`, `previsa temp …`).

```mermaid
flowchart LR
  T["tokens<br/>com faz bol cenour"] --> K["para k = 2 … mín(n, 5)<br/>pega os k primeiros tokens"]
  K --> P["com faz<br/>com faz bol<br/>com faz bol cenour"]
```

**Vilela · `contiguous`**: toda sequência de palavras, em qualquer lugar da busca. Acha o miolo de buscas que
começam diferente.

```mermaid
flowchart LR
  T["tokens<br/>com faz bol cenour"] --> W["janelas de 2 … n-1 tokens<br/>(por tamanho, depois posição)<br/>+ a busca inteira"]
  W --> E{"já é padrão<br/>do Viana?"}
  E -- "sim: com faz,<br/>com faz bol cenour" --> X["descartado"]
  E -- não --> P["faz bol<br/>bol cenour<br/>faz bol cenour"]
```

**Oliveira · `skipgram`**: de 2 a 4 palavras na ordem, com saltos. Junta buscas que têm palavras a mais no meio
(*como fazer **um** bolo de cenoura*).

```mermaid
flowchart LR
  T["tokens<br/>com faz bol cenour"] --> C["toda combinação de 2, 3, 4<br/>posições, mantendo a ordem"]
  C --> E{"já é Viana<br/>ou Vilela?"}
  E -- sim --> X["descartado"]
  E -- não --> P["com bol · com cenour<br/>faz cenour · com faz cenour<br/>com bol cenour"]
```

**Dourado · `bag`**: as mesmas combinações, sem a ordem. Junta *bolo de cenoura como fazer* com *como fazer bolo
de cenoura*.

```mermaid
flowchart LR
  T["tokens<br/>com faz bol cenour"] --> C["combinações de 2, 3, 4"]
  C --> D{"palavras todas<br/>diferentes?"}
  D -- não --> X["descartado"]
  D -- sim --> S["ordena as palavras<br/>alfabeticamente"]
  S --> E{"já é Viana, Vilela<br/>ou Oliveira?"}
  E -- sim --> X
  E -- não --> P["bol com · cenour faz<br/>bol cenour com faz …"]
```

**Troca de entidades** (comum aos três seguintes)

```mermaid
flowchart LR
  A["hotéis baratos em salvador"] --> L["em cada palavra, tenta primeiro<br/>a chave mais longa do gazetteer<br/>(sem acento e sem caixa;<br/>chaves de até 3 letras exatas)"]
  L --> H{"casou?"}
  H -- "hotéis → Turismo" --> P1["[turismo]"]
  H -- "salvador → Local > Cidade" --> P2["[local>cidade]"]
  H -- não --> W["a palavra vai<br/>para o tokenizador"]
  P1 & P2 & W --> R["[turismo] barat [local>cidade]<br/>(cada marcador é um token)"]
```

**Barbosa · `entity_prefix`**: o Viana sobre a busca com entidades trocadas.

```mermaid
flowchart LR
  R["[turismo] barat [local>cidade]"] --> K["prefixos de 2 … 5 tokens"]
  K --> B{"tem entidade?"}
  B -- não --> X["descartado"]
  B -- sim --> E["expande: um padrão por tipo<br/>[local>cidade] → [local] e [cidade]"]
  E --> C["junta repetições:<br/>x [local] [local] → x [local]"]
  C --> P["[turismo] barat<br/>[turismo] barat [local]<br/>[turismo] barat [cidade]"]
```

**Figueredo · `entity_contiguous`**: o Vilela sobre a busca trocada, sem o que o Barbosa achou.

```mermaid
flowchart LR
  R["[turismo] barat [local>cidade]"] --> W["sequências de 2 … n-1 tokens<br/>+ a busca inteira"]
  W --> B{"tem entidade?"}
  B -- não --> X["descartado"]
  B -- sim --> E["expande por tipo,<br/>junta repetições"]
  E --> D{"já é<br/>Barbosa?"}
  D -- sim --> X
  D -- não --> P["barat [local]<br/>barat [cidade]"]
```

**Rosa · `entity_skipgram`**: o Oliveira sobre a busca trocada, sem o que Barbosa e Figueredo acharam. Escrito em
fevereiro de 2019 exatamente para esta busca: *hotéis baratos em salvador* → `[turismo] [local]`.

```mermaid
flowchart LR
  R["[turismo] barat [local>cidade]"] --> C["combinações de 2, 3, 4<br/>posições, mantendo a ordem"]
  C --> B{"tem entidade?"}
  B -- não --> X["descartado"]
  B -- sim --> E["expande por tipo,<br/>junta repetições"]
  E --> D{"já é Barbosa<br/>ou Figueredo?"}
  D -- sim --> X
  D -- não --> P["[turismo] [local]<br/>[turismo] [cidade]"]
```

Depois o [`aggregate`](spec/ALGORITHMS.md#6-aggregation-aggregate) agrupa os padrões de todas as buscas: número de
buscas, volume total, concorrência e CPC médios, a busca líder e as faixas de 1 a 20.

## A história

Em 2018 o autor tinha milhões de buscas do Google com volume, CPC e concorrência e precisava saber que sites
construir. Ler busca por busca era inviável, então ele escreveu algoritmos que acham o que as buscas têm em comum,
primeiro em T-SQL, depois em C#: Dourado (saco de palavras) primeiro, Vilela em outubro de 2018, Viana e Barbosa em
janeiro de 2019, Rosa em fevereiro de 2019 (escrito para *hotéis baratos em salvador* → `[turismo] [local]`). Depois
ele rotulou à mão 154 mil padrões com o jeito de ganhar dinheiro com cada um. O resultado está publicado no
[google-keywords-dataset](https://github.com/CaiqueDourado/google-keywords-dataset)
([Kaggle](https://www.kaggle.com/datasets/caiquedourado/google-keywords-dataset)): 37,96 milhões de buscas e
1,73 milhão de padrões de cinco desses algoritmos. Esta biblioteca é aquele código, reescrito do zero, testado, com
os bugs corrigidos por padrão e mantidos quando pedido.

## Limites

- Só português do Brasil. Os mercados de fora do Brasil do dataset usaram o Snowball de inglês e espanhol; esse
  passo não está incluído (tokenize o texto por conta própria e chame os geradores).
- O padrão pai generalizado (`padrao_pai`) da tabela publicada não foi implementado: a regra original não foi
  encontrada.
- O stemmer é agressivo de propósito (`hotéis` → `hot`), como no original; agrupa bem, mas lê mal. Mostre o
  `top_search` para pessoas, não o padrão.
- Skip-grams e sacos de palavras crescem rápido em buscas longas (até 4 palavras de n); buscas com mais de 150
  caracteres não geram nada, como no original.

## Relacionados

- [portuguese-entity-gazetteer](https://github.com/CaiqueDourado/portuguese-entity-gazetteer): 307 mil entidades do português com seus tipos, prontas para carregar como gazetteer ([Kaggle](https://www.kaggle.com/datasets/caiquedourado/portuguese-entity-gazetteer)).
- [google-keywords-dataset](https://github.com/CaiqueDourado/google-keywords-dataset): 38 milhões de buscas e os 1,73 milhão de padrões que estes algoritmos produziram ([Kaggle](https://www.kaggle.com/datasets/caiquedourado/google-keywords-dataset)).
- [DedupKit](https://github.com/CaiqueDourado/DedupKit): a mesma família de chaves (acentos, radicais, chave fonética) aplicada a nomes de produto.
- [dicionario-portugues](https://github.com/CaiqueDourado/dicionario-portugues): 272 mil palavras do português com radicais e chaves fonéticas.
- [municipios-e-estados-do-brasil](https://github.com/CaiqueDourado/municipios-e-estados-do-brasil): os lugares do gazetteer de exemplo.

## Repositório

```text
python/        pacote search-pattern-miner (src/, tests/)
dotnet/        SearchPatternMiner, os testes e um exemplo (MinePatterns)
spec/          ALGORITHMS.md: todas as regras, o contrato que as duas linguagens seguem
testdata/      golden.json, os valores que os dois conjuntos de testes conferem
data/          gazetteer-pt.tsv, o gazetteer de exemplo
examples/      keywords.csv, buscas sintéticas com métricas inventadas
evaluation/    reprodução dos padrões publicados
tools/         make_testdata.py, make_gazetteer.py, make_examples.py
docs/ scripts/ capa e imagem do JSON
```

Licença: [MIT](LICENSE). O stemmer é uma porta do BrazilianStemmer do Lucene (Apache 2.0); veja
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

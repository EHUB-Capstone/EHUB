# Offline ranker evaluation

Runs the actual Application `MentorRankingModel.FromJson` validator and `TrainedMentorFitScorer` on caller-provided local inputs. It does not open a database, train a model, or call Ollama/semantic services. Supply only candidates whose eligibility has already been checked; this runner measures scoring rather than authorization or allocation rules.

```powershell
dotnet run --project tools/mentor-matching/evaluate/Evaluate.csproj -- --profiles <snapshot.jsonl> --projects <projects.json> --model <model.json> --output <evaluation.json>
```

Profiles are JSONL rows containing `mentorRef` and `profile` with the existing Business/IT profile fields. Unknown envelope/profile fields are ignored, and only professional scoring fields are mapped into the runtime entity. References must be unique ASCII codes, not names or contacts. Projects use this shape (a top-level array is also accepted):

```json
{"projects":[{"projectId":"example-project","title":"Cửa hàng minh họa","description":"Cần tư vấn marketing và thử nghiệm kênh bán hàng."}]}
```

Project text follows the API handler: description, newline, title. Every output includes model/input/assembly SHA-256 hashes, all candidate scores, evidence-gated recommendations and measured warm latency. Ordering uses the unrounded runtime value, then reference ascending for exact ties. Reasons contain the runtime evidence explanation. No raw mentor prose is copied into the report.

Optional `--labels <labels.jsonl>` calculates NDCG@5 (gain `2^grade-1`), Recall@3, Precision@3 and top-one relevance. Label rows need `projectId`, `mentorRef`, integer `relevance` 0–3. Every project/candidate pair must be present exactly once; missing labels are rejected instead of treated as zero. Relevance >=2 counts as relevant. Precision@3 always divides by three, including short recommendation lists. Zero ideal gain yields null NDCG; projects with no relevant candidates are excluded from average Recall/Precision/top-one metrics. All-zero label projects receive a separate abstention measure. Scores are not probabilities, and assistant-authored scenario labels are not independent evidence of real suitability.

Keep profiles, labels and reports outside Git. Compare candidate and baseline using exactly the same files. The CLI prints aggregate counts and model hash only; malformed inputs fail without echoing their contents or environment paths.

Run `pwsh -File tools/mentor-matching/evaluate/smoke.ps1` to build and check the evaluator with fictional temporary inputs. It covers evidence filtering, practical experience versus education, abstention, metric arithmetic, repeatable ordering, exact model/input hashes, candidate-model selection, invalid/missing roles, duplicate IDs, incomplete labels, and prevention of input overwrite. The fixtures are created outside Git and removed after the checks.

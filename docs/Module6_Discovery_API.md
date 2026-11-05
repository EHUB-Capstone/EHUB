# Module 6 — Discovery API

`GET /api/showcase` requires authentication and returns `ApiResponse<PagedResponse<ShowcaseCardDto>>`.
Only non-deleted Published showcases with an existing project profile are discoverable.

| Query | Default | Rule |
| --- | --- | --- |
| search | omitted | Literal case-insensitive substring of the latest Project Name or Description; at most 100 characters |
| stage | omitted | Idea, Mvp, Beta, Launched; case-insensitive |
| industry | omitted | Exact normalized startup-industry name, at most 100 characters; matches ProjectTags of type StartupField |
| semesterId | omitted | Non-empty GUID; semester of the latest Project |
| sort | newest | newest: PublishedAt descending; popular: LikeCount descending, then PublishedAt descending |
| page | 1 | Positive integer, at most int.MaxValue / 50 to prevent offset overflow |
| pageSize | 20 | Between 1 and 50 |

The latest project is selected by CreatedAt descending and Id descending before search or filtering.
Each lineage yields at most one card. All filters combine with AND. Final sorting uses showcase Id
as a stable tie-breaker. An unknown industry/semester or a page beyond the last page returns an empty list.

Card fields: id, projectName, description, logoUrl, developmentStage, publishedAt, teamName,
industries, likeCount, commentCount. Industries come from the latest Project's StartupField tags,
matching the existing Project Data representation. Review notes, review history and member data
are not part of this DTO.

Pagination follows the existing PagedResponse contract: items, pageIndex, pageSize, totalItems, totalPages.
The request parameter is `page`; the response field is `pageIndex`.

Example: `/api/showcase?search=garden&stage=Mvp&industry=AgriTech&sort=popular&page=1&pageSize=12`.

Invalid query values return 400; unauthenticated requests return 401. There is no class/role restriction
on discovery for authenticated users. Existing owner/moderation access rules remain on their own endpoints.

The existing status and published_at indexes are reused; this task adds no migration.
`GET /api/showcase/filter-options` also requires authentication and returns an ApiResponse with
industries (active catalog names) and semesters (id, code, name). Choices are independent of
showcase results: all active, non-deleted startup industries and all non-deleted semesters
(including planned and archived semesters) are returned, even when they have no Published showcase.
Student and Mentor can use these filters without staff-only APIs. Stage always offers all four stages.

The Gallery calls showcaseApi.list, getFilterOptions and get for the detail modal. Search, filters,
sort and paging run on the server. Featured/Trending are disabled pending their own APIs; interaction
counts are read-only until Upvote is implemented.

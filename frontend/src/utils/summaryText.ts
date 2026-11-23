import i18n from '../i18n/index.ts';
import { formatNumber as fmtNumber } from '../i18n/format.ts';
// Every word of the AI summary screen that the application itself writes, in one place. What the AI writes about a submission
// (overview, key points, suggested questions) and the sentences quoted from the files stay in the language of the submission; this
// file is only the interface around them. The project has no translation library, so the strings are plain constants.

/** "1 file" / "2 files": the noun agrees with the count. */
export const plural = (count: number, one: string, many = `${one}s`) => (count === 1 ? one : many);

export const summaryText = {
  common: {
    get retry() { return i18n.t('utils:summaryText.retry'); },
    get open() { return i18n.t('utils:summaryText.open'); },
    get addToFeedback() { return i18n.t('utils:summaryText.addToFeedback'); },
    get feedback() { return i18n.t('utils:summaryText.feedback'); },
    get openFile() { return i18n.t('utils:summaryText.openFile'); },
  },

  card: {
    get title() { return i18n.t('utils:summaryText.aiSummary'); },
    get loading() { return i18n.t('utils:summaryText.loadingAiSummary'); },
    get noFiles() { return i18n.t('utils:summaryText.theTeamHasNotSubmittedAny'); },
    get filesLabel() { return i18n.t('utils:summaryText.files'); },
    selectAll: (count: number) => `Select all (${count})`,
    get clearSelection() { return i18n.t('utils:summaryText.clearSelection'); },
    summarise: (count: number) => `Summarise ${count} ${plural(count, 'file')}`,
    get chooseFiles() { return i18n.t('utils:summaryText.selectFilesToSummarise'); },
    get allSummarised() { return i18n.t('utils:summaryText.everyFileIsSummarisedNewUploads'); },
    get cancel() { return i18n.t('common:actions.cancel'); },
    get queueNote() { return i18n.t('utils:summaryText.theSystemSummarisesOneFileAt'); },
    queued: (count: number) => `Queued ${count} ${plural(count, 'file')}. Each summary appears in its row when ready.`,
    get requeued() { return i18n.t('utils:summaryText.queuedTheFileAgainItsSummary'); },
    get cancelled() { return i18n.t('utils:summaryText.aiSummaryCancelledFinishedSummariesAre'); },
    get loadFailedSr() { return i18n.t('utils:summaryText.couldNotLoadTheAiSummary'); },
    progressSr: (ready: number, total: number, working: number) => `${ready} of ${total} files summarised, ${working} in progress.`,
    get needsAttention() { return i18n.t('utils:summaryDiagnostics.needsAttention'); },
    get overviewTitle() { return i18n.t('utils:summaryText.checkpointOverview'); },
    overviewRequirements: (found: number, total: number) => `Requirements ${found} of ${total} found`,
    get overviewReuploaded() { return i18n.t('utils:summaryText.filesReUploaded'); },
    overviewLinks: (count: number) => `${count} ${plural(count, 'link')}`,
    get requirementsTitle() { return i18n.t('utils:summaryText.requirementsRubric'); },
    get requirementsNote() { return i18n.t('utils:summaryText.combinedFromTheFilesReadQuotes'); },
    get linksTitle() { return i18n.t('utils:summaryText.links'); },
    get linksNote() { return i18n.t('utils:summaryText.theAiDoesNotOpenLinks'); },
    comparedNote: (compared: string) => `Figures were compared with ${compared}: no differences found.`,
    uncheckedFiles: (names: string[]) =>
      `Could not compare ${names.length} ${plural(names.length, 'file')} because no figures were read from ${plural(names.length, 'it', 'them')}: ${names.join(', ')}.`,
  },

  file: {
    get quickViewWaiting() { return i18n.t('utils:summaryText.showingAQuickViewBuiltFrom'); },
    get overviewFromSource() { return i18n.t('utils:summaryText.theOverviewIsQuotedFromThe'); },
    get overviewSources() { return i18n.t('utils:summaryText.sourcesOfTheOverview'); },
    get showEvidence() { return i18n.t('utils:summaryText.showEvidence'); },
    get keySentences() { return i18n.t('utils:summaryText.keySentences'); },
    get questions() { return i18n.t('utils:summaryText.suggestedQuestionsForTheTeam'); },
    get statusLines() { return i18n.t('utils:summaryText.statusReportedInTheFile'); },
    get needsRefresh() { return i18n.t('utils:summaryText.theCriteriaOrTheAnalysisWere'); },
    get regenerate() { return i18n.t('utils:summaryText.regenerate'); },
    regenerateAria: (name: string) => `Regenerate the summary of ${name}`,
    retryAria: (name: string) => `Retry the summary of ${name}`,
    tickAria: (name: string) => `Summarise ${name}`,
    longFile: (characters: number) => `Long file: the AI only read the most important sentences (about ${fmtNumber(characters)} characters). `,
    summarisedAt: (time: string) => `Summarised at ${time}.`,
    evidenceByRequirement: (found: number, total: number) => `Evidence by requirement (${found} of ${total} found)`,
    get littleText() { return i18n.t('utils:summaryText.thisFileIsMostlyImagesOr'); },
  },

  requirements: {
    foundOf: (found: number, total: number) => `${found} of ${total} found in the files`,
    get rubricCriteria() { return i18n.t('utils:summaryText.rubricCriteria'); },
    get checkpointRequirements() { return i18n.t('utils:summaryText.checkpointRequirements'); },
    notFoundGroup: (count: number) => `Not found in the files (${count})`,
    notCheckedGroup: (count: number) => `Not checked yet (${count})`,
    nothingFound: (scope: 'file' | 'files', titles: string[]) =>
      `${scope === 'file' ? 'This file does not mention' : 'None of the files read mention'} any of the ${titles.length} requirements and rubric criteria (${titles.join(', ')}).`,
    get expectedContent() { return i18n.t('utils:summaryText.expectedContent'); },
    get mentioned() { return i18n.t('utils:summaryText.mentioned'); },
    get notMentioned() { return i18n.t('utils:summaryText.notMentioned'); },
    get cueMentionedNoQuote() { return i18n.t('utils:summaryText.theFileMentionsThisButHas'); },
    get cueNotMentioned() { return i18n.t('utils:summaryText.theFileDoesNotMentionThis'); },
    kind: { get Requirement() { return i18n.t('utils:summaryText.requirement'); }, get Criterion() { return i18n.t('utils:summaryText.rubricCriterion'); } } as Record<string, string>,
  },

  findings: {
    Related: { get label() { return i18n.t('utils:summaryText.relatedContentFound'); }, get hint() { return i18n.t('utils:summaryText.mentionedInTheFilesButNo'); } },
    NotFound: { get label() { return i18n.t('utils:summaryText.notFoundInTheFiles'); }, get hint() { return i18n.t('utils:summaryText.noneOfTheFilesReadMention'); } },
    NotChecked: { get label() { return i18n.t('utils:summaryText.notCheckedYet'); }, get hint() { return i18n.t('utils:summaryText.summariseAFileToCheckThis'); } },
    Live: { get label() { return i18n.t('utils:summaryText.assessedLive'); }, get hint() { return i18n.t('utils:summaryText.presentationQAAndTeamworkAre'); } },
    Product: { get label() { return i18n.t('utils:summaryText.checkTheProduct'); }, get hint() { return i18n.t('utils:summaryText.assessedOnTheProductOrDemo'); } },
    Overall: { get label() { return i18n.t('utils:summaryText.judgedOnTheWholeFile'); }, get hint() { return i18n.t('utils:summaryText.judgedOnHowClearAndComplete'); } },
  },

  status: {
    get queued() { return i18n.t('utils:summaryText.queued'); },
    get autoQueued() { return i18n.t('utils:summaryText.autoQueued'); },
    ahead: (count: number) => `${count} ahead`,
    get summarising() { return i18n.t('utils:summaryText.summarising'); },
    get summarised() { return i18n.t('utils:summaryText.summarised'); },
    get needsUpdate() { return i18n.t('utils:summaryText.needsUpdate'); },
    get failed() { return i18n.t('utils:summaryText.failed'); },
    get cancelled() { return i18n.t('utils:summaryText.cancelled'); },
    get notSummarised() { return i18n.t('utils:summaryText.notSummarised'); },
    get couldNotLoad() { return i18n.t('utils:summaryText.couldNotLoad'); },
    get checking() { return i18n.t('utils:summaryText.checking'); },
    get noFiles() { return i18n.t('utils:summaryText.noFiles'); },
    get oneSummarised() { return i18n.t('utils:summaryText.1FileSummarised'); },
    allSummarised: (count: number) => `All ${count} files summarised`,
    needUpdating: (count: number) => `${count} need updating`,
    summarisingCount: (count: number) => `${count} summarising`,
    queuedCount: (count: number) => `${count} queued`,
    failedCount: (count: number) => `${count} failed`,
    readyOfTotal: (ready: number, total: number) => `${ready}/${total} summarised`,
    notSummarisedCount: (count: number) => `${count} not summarised`,
    get almostDone() { return i18n.t('utils:summaryText.almostDone'); },
    minutesLeft: (minutes: number) => `About ${minutes} min left`,
    reported: { get Done() { return i18n.t('utils:summaryText.done'); }, get InProgress() { return i18n.t('utils:summaryText.inProgress'); }, get Pending() { return i18n.t('utils:summaryText.pending'); } },
  },

  notices: {
    ignoredInstructions: (count: number) =>
      `This file contains ${count} ${plural(count, 'sentence')} addressed to the AI, such as commands or requests for a grade. ${plural(count, 'It was', 'They were')} ignored: the summary neither uses nor quotes ${plural(count, 'it', 'them')}. Please read the file.`,
  },

  figures: {
    get earlierTitle() { return i18n.t('utils:summaryText.figuresThatDifferFromEarlierCheckpoints'); },
    get acrossFilesTitle() { return i18n.t('utils:summaryText.figuresThatDifferBetweenFiles'); },
    get insideFileTitle() { return i18n.t('utils:summaryText.conflictingFigures'); },
    get earlierNote() { return i18n.t('utils:summaryText.thisCheckpointStatesADifferentNumber'); },
    get acrossFilesNote() { return i18n.t('utils:summaryText.twoFilesGiveDifferentValuesFor'); },
    get insideFileNote() { return i18n.t('utils:summaryText.theFileGivesTheseFiguresWith'); },
  },

  feedback: {
    get question() { return i18n.t('utils:summaryText.wasThisSummaryUseful'); },
    get group() { return i18n.t('utils:summaryText.rateThisSummary'); },
    get useful() { return i18n.t('utils:summaryText.useful'); },
    get notUseful() { return i18n.t('utils:summaryText.notUseful'); },
    get saved() { return i18n.t('utils:summaryText.yourFeedbackWasSaved'); },
    get why() { return i18n.t('utils:summaryText.whyOptional'); },
    get whyGroup() { return i18n.t('utils:summaryText.whyItWasNotUseful'); },
    reasons: {
      get Inaccurate() { return i18n.t('utils:summaryText.inaccurateInformation'); },
      get MissingKeyPoint() { return i18n.t('utils:summaryText.missingKeyPoint'); },
      get WrongEvidence() { return i18n.t('utils:summaryText.wrongQuote'); },
      get Other() { return i18n.t('utils:summaryText.otherReason'); },
    },
  },

  quote: {
    page: (n: number) => `page ${n}`,
    slide: (n: number) => `slide ${n}`,
    openAt: (where: string) => `Open ${where}`,
  },

  versions: {
    get title() { return i18n.t('utils:summaryText.changesSinceThePreviousVersion'); },
    get intro() { return i18n.t('utils:summaryText.theTeamUploadedSomeFilesAgain'); },
    get compare() { return i18n.t('utils:summaryText.compare'); },
    get comparing() { return i18n.t('utils:summaryText.comparing'); },
    get none() { return i18n.t('utils:summaryText.noFileHasBeenUploadedAgain'); },
    get failed() { return i18n.t('utils:summaryText.couldNotCompareTheVersionsPlease'); },
    get edited() { return i18n.t('utils:summaryText.edited'); },
    get added() { return i18n.t('utils:summaryText.added'); },
    get removed() { return i18n.t('utils:summaryText.removed'); },
    get figuresChanged() { return i18n.t('utils:summaryText.figuresChanged'); },
    changedCount: (count: number) => `${count} ${plural(count, 'sentence')} edited`,
    addedCount: (count: number) => `${count} added`,
    removedCount: (count: number) => `${count} removed`,
    get newFile() { return i18n.t('utils:summaryText.newFileNoEarlierVersionTo'); },
    unchanged: (from: number, to: number) => `v${to} is the same as v${from}: no content changes.`,
    unreadable: (from: number, to: number) => `One of the two versions (v${from}, v${to}) could not be read, so they cannot be compared.`,
    comparedWith: (from: number, percent: number) => `Compared with v${from}: ${percent}% of the content is unchanged.`,
    figureSentences: (count: number) => `${count} ${plural(count, 'sentence')} with changed figures.`,
  },

  errors: {
    get WORKSPACE_CONCURRENCY_CONFLICT() { return i18n.t('utils:summaryText.theSummaryWasUpdatedPleaseReload'); },
    get WORKSPACE_SUMMARY_DISABLED() { return i18n.t('utils:summaryText.aiSummariesAreNotEnabledOn'); },
    get WORKSPACE_SUMMARY_QUEUE_FULL() { return i18n.t('utils:summaryText.manyFilesAreWaitingToBe'); },
    get WORKSPACE_SUMMARY_UNREADABLE() { return i18n.t('utils:summaryText.theTextOfThisFileCould'); },
    get WORKSPACE_SUMMARY_UNAVAILABLE() { return i18n.t('utils:summaryText.theAiServiceHasNotFinished'); },
    get WORKSPACE_SUMMARY_MODEL_UNREACHABLE() { return i18n.t('utils:summaryText.theAiServiceIsNotReachable'); },
    get WORKSPACE_SUMMARY_MODEL_TIMEOUT() { return i18n.t('utils:summaryText.theAiServiceTookTooLong'); },
    get WORKSPACE_SUMMARY_STORAGE_UNAVAILABLE() { return i18n.t('utils:summaryText.theFileCouldNotBeLoaded'); },
    get WORKSPACE_SUMMARY_MODEL_BAD_ANSWER() { return i18n.t('utils:summaryText.theAiReturnedAnAnswerThat'); },
    get WORKSPACE_SUMMARY_NO_CONTENT() { return i18n.t('utils:summaryText.thisFileIsNoLongerPart'); },
    get WORKSPACE_NOT_FOUND() { return i18n.t('utils:summaryText.someFilesChangedWhileYouWere'); },
    get WORKSPACE_VALIDATION_ERROR() { return i18n.t('utils:summaryText.chooseBetweenOneAndFiveFiles'); },
    get AUTH_RATE_LIMITED() { return i18n.t('utils:summaryText.youHaveRequestedTooManySummaries'); },
    get WORKSPACE_ACCESS_DENIED() { return i18n.t('utils:summaryText.youDoNotHaveAccessTo'); },
  } as Record<string, string>,
  get genericError() { return i18n.t('utils:summaryText.theAiSummaryCouldNotBe'); },
} as const;

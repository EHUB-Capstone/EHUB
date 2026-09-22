import type {
  ProjectProposal,
  ProjectProposalContent,
  ProjectProposalDraft,
  ProjectProposalStatus,
} from '../types/projectProposal';

export const PROJECT_PROPOSAL_MAX_TOTAL_LENGTH = 30_000;

export const projectProposalFields = [
  { name: 'title', label: 'Proposal title', type: 'text', maxLength: 200, submitMinimum: 5, placeholder: 'A clear title for the project proposal' },
  { name: 'startupName', label: 'Startup name', type: 'text', maxLength: 150, submitMinimum: 2, placeholder: 'Enter the startup or product name' },
  { name: 'tagline', label: 'Tagline', type: 'text', maxLength: 200, submitMinimum: 0, placeholder: 'A short slogan or description of the idea' },
  { name: 'problem', label: 'Problem statement', type: 'textarea', maxLength: 3000, submitMinimum: 100, placeholder: 'Describe the problem and the evidence that it matters.' },
  { name: 'solution', label: 'Proposed solution', type: 'textarea', maxLength: 3000, submitMinimum: 100, placeholder: 'Describe the product or service and how it solves the problem.' },
  { name: 'targetCustomers', label: 'Target customers', type: 'textarea', maxLength: 2000, submitMinimum: 50, placeholder: 'Who are the users and paying customers?' },
  { name: 'valueProposition', label: 'Value proposition', type: 'textarea', maxLength: 2000, submitMinimum: 50, placeholder: 'What distinct value will customers receive?' },
  { name: 'marketSize', label: 'Market size and potential', type: 'textarea', maxLength: 2500, submitMinimum: 0, placeholder: 'Describe TAM, SAM, SOM, or other market evidence.' },
  { name: 'competitors', label: 'Competitors and advantage', type: 'textarea', maxLength: 3000, submitMinimum: 0, placeholder: 'Describe alternatives, competitors, and the competitive advantage.' },
  { name: 'businessModel', label: 'Business model', type: 'textarea', maxLength: 3000, submitMinimum: 100, placeholder: 'Explain how the venture creates and delivers value.' },
  { name: 'revenueModel', label: 'Revenue model', type: 'textarea', maxLength: 2000, submitMinimum: 0, placeholder: 'Explain pricing and expected revenue streams.' },
  { name: 'marketingStrategy', label: 'Marketing and sales strategy', type: 'textarea', maxLength: 3000, submitMinimum: 0, placeholder: 'Explain how customers will be acquired and retained.' },
  { name: 'technology', label: 'Technology', type: 'textarea', maxLength: 3000, submitMinimum: 0, placeholder: 'Describe the proposed technology and architecture.' },
  { name: 'financialPlan', label: 'Financial plan', type: 'textarea', maxLength: 3000, submitMinimum: 0, placeholder: 'Describe costs, assumptions, and financial milestones.' },
  { name: 'roadmap', label: 'Roadmap and milestones', type: 'textarea', maxLength: 3000, submitMinimum: 100, placeholder: 'Describe phases, milestones, and expected outcomes.' },
  { name: 'teamIntroduction', label: 'Team introduction and roles', type: 'textarea', maxLength: 2000, submitMinimum: 0, placeholder: 'Introduce team members and their responsibilities.' },
] as const satisfies ReadonlyArray<{
  name: keyof ProjectProposalContent;
  label: string;
  type: 'text' | 'textarea';
  maxLength: number;
  submitMinimum: number;
  placeholder: string;
}>;

export const emptyProjectProposalDraft: ProjectProposalDraft = {
  title: '',
  startupName: '',
  tagline: '',
  problem: '',
  solution: '',
  targetCustomers: '',
  valueProposition: '',
  marketSize: '',
  competitors: '',
  businessModel: '',
  revenueModel: '',
  marketingStrategy: '',
  technology: '',
  financialPlan: '',
  roadmap: '',
  teamIntroduction: '',
  changeNote: '',
};

export type ProjectProposalFieldErrors = Partial<Record<keyof ProjectProposalDraft, string>>;

export function toProjectProposalDraft(proposal: ProjectProposal | null): ProjectProposalDraft {
  if (!proposal) return { ...emptyProjectProposalDraft };
  const result = { ...emptyProjectProposalDraft };
  for (const field of projectProposalFields) result[field.name] = proposal[field.name] || '';
  return result;
}

export function toProjectProposalContent(draft: ProjectProposalDraft): ProjectProposalContent {
  return Object.fromEntries(
    projectProposalFields.map((field) => [field.name, draft[field.name].trim()]),
  ) as unknown as ProjectProposalContent;
}

export function proposalHasContentChanges(proposal: ProjectProposal | null, draft: ProjectProposalDraft): boolean {
  if (!proposal) return projectProposalFields.some((field) => draft[field.name].trim().length > 0);
  return projectProposalFields.some((field) => proposal[field.name].trim() !== draft[field.name].trim());
}

export function validateProjectProposalDraft(draft: ProjectProposalDraft): ProjectProposalFieldErrors {
  const errors: ProjectProposalFieldErrors = {};
  let totalLength = 0;
  for (const field of projectProposalFields) {
    const value = draft[field.name].trim();
    totalLength += value.length;
    if (value.length > field.maxLength) errors[field.name] = `Maximum ${field.maxLength} characters.`;
  }
  if (draft.changeNote.trim().length > 1000) errors.changeNote = 'Maximum 1000 characters.';
  if (totalLength > PROJECT_PROPOSAL_MAX_TOTAL_LENGTH) {
    errors.title = `Proposal content must not exceed ${PROJECT_PROPOSAL_MAX_TOTAL_LENGTH.toLocaleString()} characters in total.`;
  }
  return errors;
}

export function validateProjectProposalSubmission(proposal: ProjectProposal): ProjectProposalFieldErrors {
  const draft = toProjectProposalDraft(proposal);
  const errors = validateProjectProposalDraft(draft);
  for (const field of projectProposalFields) {
    if (field.submitMinimum > 0 && proposal[field.name].trim().length < field.submitMinimum) {
      errors[field.name] = `At least ${field.submitMinimum} characters are required before submission.`;
    }
  }
  return errors;
}

export const projectProposalStatusLabel: Record<ProjectProposalStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted for review',
  NeedsRevision: 'Needs revision',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Archived: 'Archived',
};

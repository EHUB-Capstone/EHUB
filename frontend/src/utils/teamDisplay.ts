// @ts-nocheck
const clean = (value) => (typeof value === 'string' ? value.trim() : '');

export const isLegacyClassCodeGroupName = (team) => {
  const groupName = clean(team?.groupName).toUpperCase();
  const teamCode = clean(team?.teamCode).toUpperCase();
  return Boolean(groupName && teamCode.startsWith(`${groupName}_TEAM_`));
};

export const getDisplayGroupName = (team) => {
  const groupName = clean(team?.groupName);
  const projectName = clean(team?.projectName);

  if (projectName && isLegacyClassCodeGroupName(team)) return projectName;
  return groupName || projectName;
};

export const getDisplayTeamName = (team) => (
  clean(team?.projectName)
  || getDisplayGroupName(team)
  || clean(team?.teamName)
);

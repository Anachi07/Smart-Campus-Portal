// Keep installation controls available without placing an app banner above the school masthead.
document.addEventListener('DOMContentLoaded', () => {
  const tools = document.querySelector('.campus-app-tools');
  const footer = document.querySelector('.school-footer');
  if (tools && footer) footer.append(tools);
});

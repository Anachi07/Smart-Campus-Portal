// Keep installation controls available without placing an app banner above the school masthead.
document.addEventListener('DOMContentLoaded', () => {
  const preferences = document.querySelector('.campus-accessibility');
  const navigation = document.querySelector('.school-navigation nav');
  if (preferences && navigation) navigation.append(preferences);
  const tools = document.querySelector('.campus-app-tools');
  const footer = document.querySelector('.school-footer');
  if (tools && footer) footer.append(tools);
});

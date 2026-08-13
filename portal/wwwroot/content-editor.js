(() => {
  const form = document.querySelector('[data-content-editor]');
  const source = form?.querySelector('[data-content-json]');
  const host = form?.querySelector('#content-rich-editors');
  if (!form || !source || !host || typeof window.Quill !== 'function') return;

  let documentValue;
  try { documentValue = JSON.parse(source.value); }
  catch { host.textContent = 'The current content document is not valid JSON.'; source.hidden = false; return; }

  const labels = value => value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, letter => letter.toUpperCase());
  const blankFrom = value => {
    if (Array.isArray(value)) return [];
    if (value && typeof value === 'object') {
      if (Object.keys(value).length === 1 && typeof value.deltaJson === 'string') {
        return { deltaJson: '{"ops":[{"insert":"\\n"}]}' };
      }
      return Object.fromEntries(Object.entries(value).map(([key, child]) => [key, blankFrom(child)]));
    }
    if (typeof value === 'string') return '';
    return value;
  };
  const sync = () => { source.value = JSON.stringify(documentValue); };
  const scalarInput = (parent, key, value, path) => {
    const field = document.createElement('div'); field.className = 'field';
    const label = document.createElement('label'); label.textContent = labels(key); label.htmlFor = `content-${path}`;
    const input = typeof value === 'boolean' ? document.createElement('input') : (String(value).length > 180 ? document.createElement('textarea') : document.createElement('input'));
    input.id = `content-${path}`;
    if (typeof value === 'boolean') { input.type = 'checkbox'; input.checked = value; }
    else { input.value = value ?? ''; if (/url$/i.test(key)) input.type = 'url'; else if (/date|reviewed/i.test(key) && /^\d{4}-\d{2}-\d{2}$/.test(value)) input.type = 'date'; }
    input.addEventListener('input', () => { parent[key] = typeof value === 'boolean' ? input.checked : input.value; sync(); });
    field.append(label, input); hostTarget.append(field);
  };
  const richEditor = (parent, key, value, path) => {
    const group = document.createElement('section'); group.className = 'editor-group';
    const heading = document.createElement('h2'); heading.textContent = labels(key);
    const editor = document.createElement('div'); editor.id = `editor-${path}`;
    group.append(heading, editor); hostTarget.append(group);
    const quill = new window.Quill(editor, {
      theme: 'snow',
      formats: ['header', 'bold', 'italic', 'link', 'list'],
      modules: { toolbar: [[{ header: [2, 3, false] }], ['bold', 'italic', 'link'], [{ list: 'ordered' }, { list: 'bullet' }], ['clean']] }
    });
    const toolbar = quill.getModule('toolbar')?.container;
    const commandName = element => {
      const command = [...element.classList].find(name => name.startsWith('ql-') && name !== 'ql-toolbar')?.slice(3) || 'formatting';
      const value = element.getAttribute('value') || element.dataset.value;
      if (command === 'header') return value ? `Heading level ${value}` : 'Paragraph';
      if (command === 'list') return value === 'ordered' ? 'Ordered list' : 'Bulleted list';
      if (command === 'clean') return 'Clear formatting';
      return labels(command);
    };
    toolbar?.querySelectorAll('button').forEach(button => button.setAttribute('aria-label', commandName(button)));
    toolbar?.querySelectorAll('select').forEach(select => select.setAttribute('aria-label', commandName(select)));
    toolbar?.querySelectorAll('.ql-picker-label').forEach(label => label.setAttribute('aria-label', commandName(label.closest('.ql-picker') || label)));
    toolbar?.querySelectorAll('.ql-picker-item').forEach(item => item.setAttribute('aria-label', commandName(item)));
    quill.setContents(JSON.parse(value.deltaJson));
    quill.on('text-change', () => { parent[key].deltaJson = JSON.stringify(quill.getContents()); sync(); });
  };
  const primitiveList = (parent, key, value, path) => {
    const field = document.createElement('div'); field.className = 'field';
    const label = document.createElement('label'); label.textContent = labels(key); label.htmlFor = `content-${path}`;
    const input = document.createElement('textarea'); input.id = `content-${path}`; input.value = value.join('\n');
    const hint = document.createElement('span'); hint.className = 'field-hint'; hint.textContent = 'One item per line. The published order follows this list.';
    input.addEventListener('input', () => { parent[key] = input.value.split(/\r?\n/).map(item => item.trim()).filter(Boolean); sync(); });
    field.append(label, input, hint); hostTarget.append(field);
  };
  const objectList = (parent, key, value, path) => {
    const section = document.createElement('section'); section.className = 'repeater';
    const title = document.createElement('h2'); title.textContent = labels(key); section.append(title); hostTarget.append(section);
    const render = () => {
      section.querySelectorAll('.repeater-item,.repeater-add').forEach(item => item.remove());
      value.forEach((item, index) => {
        const card = document.createElement('fieldset'); card.className = 'repeater-item';
        const legend = document.createElement('legend'); legend.textContent = `${labels(key)} ${index + 1}`; card.append(legend); section.append(card);
        const previousHost = hostTarget; hostTarget = card;
        renderObject(item, `${path}-${index}`);
        hostTarget = previousHost;
        const actions = document.createElement('div'); actions.className = 'stack-actions';
        [['Move up', -1], ['Move down', 1]].forEach(([text, offset]) => { const button=document.createElement('button');button.type='button';button.className='button button-secondary';button.textContent=text;button.disabled=(index+offset<0||index+offset>=value.length);button.addEventListener('click',()=>{[value[index],value[index+offset]]=[value[index+offset],value[index]];sync();render();});actions.append(button); });
        const remove=document.createElement('button');remove.type='button';remove.className='button button-secondary';remove.textContent='Remove';remove.addEventListener('click',()=>{value.splice(index,1);sync();render();});actions.append(remove);card.append(actions);
      });
      const add=document.createElement('button');add.type='button';add.className='button button-secondary repeater-add';add.textContent=`Add ${labels(key).replace(/s$/, '')}`;add.addEventListener('click',()=>{const sample=value[0]??{};value.push(blankFrom(sample));sync();render();});section.append(add);
    }; render();
  };
  let hostTarget = host;
  const renderObject = (value, path = 'root') => {
    Object.entries(value).forEach(([key, item]) => {
      if (typeof item === 'string' && value[`${key}RichText`]?.deltaJson) return;
      const currentHost = hostTarget;
      if (item && typeof item === 'object' && Object.keys(item).length === 1 && typeof item.deltaJson === 'string') richEditor(value, key, item, `${path}-${key}`);
      else if (Array.isArray(item) && item.every(child => typeof child === 'string')) primitiveList(value, key, item, `${path}-${key}`);
      else if (Array.isArray(item) && item.every(child => child && typeof child === 'object')) objectList(value, key, item, `${path}-${key}`);
      else if (item && typeof item === 'object') { const section=document.createElement('section');section.className='editor-group';const heading=document.createElement('h2');heading.textContent=labels(key);section.append(heading);currentHost.append(section);hostTarget=section;renderObject(item,`${path}-${key}`);hostTarget=currentHost; }
      else scalarInput(value, key, item, `${path}-${key}`);
    });
  };
  renderObject(documentValue);
  sync();
})();

chrome.action.getTitle({}).then(title => { document.querySelector("#status").textContent = title; });

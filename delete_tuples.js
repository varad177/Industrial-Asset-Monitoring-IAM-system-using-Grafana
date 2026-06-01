async function go() {
    console.log("Fetching stores...");
    const res = await fetch('http://localhost:8080/stores');
    const data = await res.json();
    const store = data.stores.find(s => s.name === 'iam-monitor');
    if(!store) { console.log('Store not found'); return; }
    
    let token = null;
    let count = 0;
    do {
        const reqBody = token ? { continuation_token: token } : {};
        const readRes = await fetch(`http://localhost:8080/stores/${store.id}/read`, {
            method: 'POST', body: JSON.stringify(reqBody), headers: { 'Content-Type': 'application/json' }
        });
        const readData = await readRes.json();
        
        if (readData.tuples && readData.tuples.length > 0) {
            const keys = readData.tuples.map(t => {
                const k = t.key || t.tuple_key || t;
                return {
                    user: k.user,
                    relation: k.relation,
                    object: k.object
                };
            });
            console.log(`Deleting ${keys.length} tuples...`);
            
            for (let i = 0; i < keys.length; i += 50) {
                const chunk = keys.slice(i, i+50);
                const delRes = await fetch(`http://localhost:8080/stores/${store.id}/write`, {
                    method: 'POST',
                    body: JSON.stringify({ deletes: { tuple_keys: chunk } }),
                    headers: { 'Content-Type': 'application/json' }
                });
                if (!delRes.ok) {
                    console.log('Error deleting chunk:', await delRes.text());
                } else {
                    count += chunk.length;
                }
            }
        }
        token = readData.continuation_token;
    } while (token && token !== "");
    
    console.log(`Successfully deleted ${count} tuples!`);
}
go().catch(console.error);

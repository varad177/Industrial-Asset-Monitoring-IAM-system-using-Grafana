const http = require('http');

async function go() {
    const res = await fetch('http://localhost:8080/stores');
    const data = await res.json();
    const store = data.stores.find(s => s.name === 'iam-monitor');
    if(!store) return;
    
    // First, write a tuple to ensure there is something
    await fetch(`http://localhost:8080/stores/${store.id}/write`, {
        method: 'POST',
        body: JSON.stringify({
            writes: {
                tuple_keys: [{ user: 'team:operators#member', relation: 'viewer', object: 'asset:asset_1' }]
            }
        }),
        headers: { 'Content-Type': 'application/json' }
    });

    // Try read with only user and relation
    const readRes = await fetch(`http://localhost:8080/stores/${store.id}/read`, {
        method: 'POST',
        body: JSON.stringify({ tuple_key: { user: 'team:operators#member', relation: 'viewer' } }),
        headers: { 'Content-Type': 'application/json' }
    });
    
    console.log("Read Status:", readRes.status);
    console.log("Read Body:", await readRes.json());
}
go().catch(console.error);

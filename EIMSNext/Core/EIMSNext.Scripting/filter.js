function MATCH(arr, checkFun) { return arr && Array.from(arr).some(checkFun); }
function MAP(arr, field) { if (!arr) return "[]"; return JSON.stringify(Array.from(arr).map(x => x[field]));}
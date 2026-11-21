import http from 'k6/http';
import { check } from 'k6';

export const options = { vus: 20, duration: '30s' };

const baseUrl = __ENV.BASE_URL || 'http://localhost:5080';
// The token is never written in the file: pass it with -e TOKEN=...
const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${__ENV.TOKEN}` };

const body = JSON.stringify({
  title: 'Laptop Lenovo ThinkPad T480, i5, 16GB RAM',
  description: 'Pentru programare, SSD 512',
  category: 'Electronics',
  condition: 'Good',
});

export default function () {
  const res = http.post(`${baseUrl}/api/v1/listings/price-suggestion`, body, { headers });
  check(res, { 'status is 200': (r) => r.status === 200 });
}

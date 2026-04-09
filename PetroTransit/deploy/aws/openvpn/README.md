# OpenVPN EC2 Runbook

This runbook sets up a dedicated OpenVPN EC2 so SSH to the PetroTransit app EC2 is not exposed to the public internet.

Final access pattern:

- OpenVPN EC2 is public on `1194/udp`
- app EC2 `22/tcp` is **not** public
- app EC2 `22/tcp` is allowed only from the VPN client subnet, default `10.8.0.0/24`
- administrators connect laptop -> OpenVPN -> app EC2 private IP

## Target shape

Recommended VPN instance:

- instance type: `t4g.nano`
- OS: Ubuntu 24.04 LTS
- network: public subnet
- Elastic IP attached

Recommended app-side assumption:

- app EC2 is in the same VPC
- app EC2 SSH is allowed only from `10.8.0.0/24`

## Security groups

OpenVPN EC2 security group:

- allow `1194/udp` from `0.0.0.0/0`
- allow `22/tcp` only from your office IP or personal management IP

App EC2 security group:

- allow `22/tcp` from `10.8.0.0/24`
- do not allow `22/tcp` from `0.0.0.0/0`

## Step 1: Install packages

```bash
sudo apt update
sudo apt install -y openvpn easy-rsa iptables-persistent
```

Create a working directory for Easy-RSA:

```bash
make-cadir ~/openvpn-ca
cd ~/openvpn-ca
```

## Step 2: Create the certificate authority

Initialize the PKI:

```bash
./easyrsa init-pki
./easyrsa build-ca
```

This creates the CA that will sign the server and client certificates.

## Step 3: Create the server certificate and key

```bash
./easyrsa gen-req server nopass
./easyrsa sign-req server server
```

Generate Diffie-Hellman parameters and a TLS auth key:

```bash
./easyrsa gen-dh
openvpn --genkey secret ta.key
```

## Step 4: Create a client certificate and key

Example for one admin client named `petrotransit-admin`:

```bash
./easyrsa gen-req petrotransit-admin nopass
./easyrsa sign-req client petrotransit-admin
```

Repeat this pattern for each administrator so each person has an individually revocable client certificate.

## Step 5: Install server artifacts

Copy the generated server files into `/etc/openvpn/server`:

```bash
sudo mkdir -p /etc/openvpn/server
sudo cp pki/ca.crt /etc/openvpn/server/
sudo cp pki/issued/server.crt /etc/openvpn/server/
sudo cp pki/private/server.key /etc/openvpn/server/
sudo cp pki/dh.pem /etc/openvpn/server/
sudo cp ta.key /etc/openvpn/server/
```

## Step 6: Create the OpenVPN server config

Create `/etc/openvpn/server/server.conf` with this baseline:

```conf
port 1194
proto udp
dev tun
ca /etc/openvpn/server/ca.crt
cert /etc/openvpn/server/server.crt
key /etc/openvpn/server/server.key
dh /etc/openvpn/server/dh.pem
tls-auth /etc/openvpn/server/ta.key 0
topology subnet
server 10.8.0.0 255.255.255.0
push "route <APP_SUBNET_CIDR> <APP_SUBNET_MASK>"
ifconfig-pool-persist /var/log/openvpn/ipp.txt
keepalive 10 120
cipher AES-256-GCM
auth SHA256
user nobody
group nogroup
persist-key
persist-tun
status /var/log/openvpn/openvpn-status.log
log-append /var/log/openvpn/openvpn.log
verb 3
explicit-exit-notify 1
```

Replace:

- `<APP_SUBNET_CIDR>` with the private subnet containing the app EC2, for example `10.0.2.0`
- `<APP_SUBNET_MASK>` with the subnet mask, for example `255.255.255.0`

If you prefer to push the full VPC CIDR instead of only the app subnet, use the VPC range instead.

## Step 7: Enable IPv4 forwarding

Edit `/etc/sysctl.conf` and ensure this line exists:

```conf
net.ipv4.ip_forward=1
```

Apply it:

```bash
sudo sysctl -p
```

Confirm it is enabled:

```bash
sysctl net.ipv4.ip_forward
```

Expected value:

```text
net.ipv4.ip_forward = 1
```

## Step 8: Configure NAT/masquerade

Find the main network interface:

```bash
ip route | awk '/default/ {print $5}'
```

Assume it returns `ens5`. Add the NAT rule:

```bash
sudo iptables -t nat -A POSTROUTING -s 10.8.0.0/24 -o ens5 -j MASQUERADE
sudo netfilter-persistent save
```

This allows VPN clients to reach resources inside the VPC through the VPN server.

## Step 9: Start and enable OpenVPN

```bash
sudo systemctl enable openvpn-server@server
sudo systemctl start openvpn-server@server
sudo systemctl status openvpn-server@server
```

The service should be active and listening on `1194/udp`.

Confirm:

```bash
sudo ss -ulpn | rg 1194
```

## Step 10: Build the client profile

Collect these files:

- `pki/ca.crt`
- `pki/issued/petrotransit-admin.crt`
- `pki/private/petrotransit-admin.key`
- `ta.key`

Create a client profile `petrotransit-admin.ovpn`:

```conf
client
dev tun
proto udp
remote <VPN_ELASTIC_IP> 1194
resolv-retry infinite
nobind
persist-key
persist-tun
remote-cert-tls server
cipher AES-256-GCM
auth SHA256
key-direction 1
verb 3
<ca>
...ca.crt contents...
</ca>
<cert>
...client cert contents...
</cert>
<key>
...client key contents...
</key>
<tls-auth>
...ta.key contents...
</tls-auth>
```

Replace `<VPN_ELASTIC_IP>` with the Elastic IP or DNS name of the OpenVPN EC2.

## Step 11: AWS routing and security checks

Validate these AWS-side assumptions:

- OpenVPN EC2 and app EC2 are in the same VPC
- app EC2 security group allows `22/tcp` from `10.8.0.0/24`
- app EC2 does not allow `22/tcp` from the internet
- route tables allow the VPN EC2 to reach the app EC2 private subnet

If the app EC2 is in the same VPC and security groups are correct, no special VPC route-table change is usually needed for basic reachability.

## Step 12: Client connection test

From your laptop:

1. Import the `.ovpn` file into the OpenVPN client.
2. Connect to the VPN.
3. Confirm the client receives an address in `10.8.0.0/24`.
4. Ping or SSH to the app EC2 private IP.

Expected SSH flow:

```bash
ssh -i <key.pem> ubuntu@<app-private-ip>
```

This should work only while connected to the VPN.

## Recovery and troubleshooting

Check service status:

```bash
sudo systemctl status openvpn-server@server
sudo journalctl -u openvpn-server@server -n 100 --no-pager
```

Check the port is listening:

```bash
sudo ss -ulpn | rg 1194
```

Check forwarding:

```bash
sysctl net.ipv4.ip_forward
```

Check NAT rules:

```bash
sudo iptables -t nat -S
```

Check the app EC2 security group:

- `22/tcp` allowed from `10.8.0.0/24`
- `22/tcp` not allowed from `0.0.0.0/0`

If VPN connects but app EC2 SSH fails:

- confirm you are targeting the app EC2 private IP, not its public IP
- confirm the app EC2 security group source is the VPN CIDR, not the VPN EC2 security group
- confirm the pushed route covers the app EC2 subnet
- confirm the OpenVPN client actually received a `10.8.0.x` address

## Operational notes

- Use one client certificate per administrator.
- Revoke individual client certificates when access should be removed.
- Keep a secure backup of the CA private key.
- OpenVPN is installed directly on the host; do not containerize this instance unless requirements change.

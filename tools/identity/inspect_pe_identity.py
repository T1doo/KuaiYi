"""Read actual PE32+ RT_MANIFEST resources; static evidence is not runtime package identity."""
import argparse
import hashlib
import json
import struct
from pathlib import Path
import xml.etree.ElementTree as ET


def inspect(path: Path, application_id: str) -> dict:
    data = path.read_bytes()
    def u16(offset):
        return struct.unpack_from('<H', data, offset)[0]
    def u32(offset):
        return struct.unpack_from('<I', data, offset)[0]
    if data[:2] != b'MZ':
        raise ValueError('Missing DOS header')
    pe = u32(60)
    if data[pe:pe+4] != b'PE\0\0' or u16(pe+4) != 0x8664 or u16(pe+24) != 0x20b:
        raise ValueError('Expected Windows x64 PE32+')
    section_count, optional_size = u16(pe+6), u16(pe+20)
    optional = pe+24
    if optional_size < 136 or u32(optional+108) < 3:
        raise ValueError('Missing resources directory')
    resource_rva = u32(optional+128)
    sections = optional+optional_size
    def rva_offset(rva):
        for index in range(section_count):
            section = sections+index*40
            virtual_size, address, raw_size, raw = struct.unpack_from('<IIII', data, section+8)
            if address <= rva < address+max(virtual_size, raw_size):
                offset = raw+rva-address
                if not 0 <= offset < len(data):
                    raise ValueError('Out-of-file RVA')
                return offset
        raise ValueError('RVA not in section')
    root = rva_offset(resource_rva)
    def entries(relative):
        offset = root+relative
        count = u16(offset+12)+u16(offset+14)
        if count > 4096:
            raise ValueError('Oversized resources directory')
        return [struct.unpack_from('<II', data, offset+16+index*8) for index in range(count)]
    manifests = []
    for kind, directory in entries(0):
        if kind != 24 or not directory & 0x80000000:
            continue
        for name, language_dir in entries(directory & 0x7fffffff):
            if name != 1 or not language_dir & 0x80000000:
                continue
            for language, leaf in entries(language_dir & 0x7fffffff):
                if leaf & 0x80000000:
                    raise ValueError('Unexpected resource depth')
                entry = root+leaf
                rva, size = u32(entry), u32(entry+4)
                if not 0 < size <= 65536:
                    raise ValueError('Manifest too large')
                start = rva_offset(rva)
                if start+size > len(data):
                    raise ValueError('Truncated manifest')
                xml = ET.fromstring(data[start:start+size].rstrip(b'\0'))
                msix = xml.findall('{urn:schemas-microsoft-com:msix.v1}msix')
                if len(msix) != 1 or msix[0].attrib != {
                    'publisher': 'CN=KuaiYi Development',
                    'packageName': 'KuaiYi.Development',
                    'applicationId': application_id,
                }:
                    raise ValueError('Actual embedded identity mismatch')
                manifests.append({'resourceType': 24, 'resourceId': name, 'languageId': language, 'applicationId': application_id, 'manifestSha256': hashlib.sha256(data[start:start+size]).hexdigest()})
    if not manifests:
        raise ValueError('Missing process activation RT_MANIFEST id 1')
    return {'file': path.name, 'sha256': hashlib.sha256(data).hexdigest(), 'pe': 'PE32+ x64', 'embeddedManifests': manifests, 'staticResult': 'Pass', 'runtimeIdentity': 'NotRun'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('install_root', type=Path)
    args = parser.parse_args()
    print(json.dumps([inspect(args.install_root/'KuaiYi.App.exe', 'KuaiYi'), inspect(args.install_root/'KuaiYi.ImageWorker.exe', 'ImageWorker')], indent=2))
